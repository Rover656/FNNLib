﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FNNLib.Messaging;
using FNNLib.Messaging.Internal;
using FNNLib.Serialization;
using FNNLib.Transports;
using UnityEngine;
using UnityEngine.Events;

namespace FNNLib.Backend {
    /// <summary>
    /// The NetworkServer class manages sending data to the clients.
    /// This is normally controlled by the NetworkManager and most games won't access this at all.
    /// This is only accessed if you wish to register custom packet types.
    ///
    /// TODO: Clean this messy pile of spaghetti
    /// </summary>
    public class NetworkServer : PacketHandler {
        /// <summary>
        /// The currently running server, if any.
        /// Check if a server is running with the IsServerRunning static property.
        /// </summary>
        public static NetworkServer instance;

        /// <summary>
        /// Whether or not a server is running.
        /// </summary>
        public static bool isServerRunning => instance != null && instance.running;

        /// <summary>
        /// Whether or not this server is running.
        /// </summary>
        public bool running => _transport != null && _transport.serverRunning && instance == this;

        /// <summary>
        /// Server started event. Raised just after the server is started.
        /// </summary>
        public UnityEvent<NetworkServer> onServerStarted = new UnityEvent<NetworkServer>();
        
        /// <summary>
        /// Server stopped event. Raised after the server is closed and clients are disconnected.
        /// </summary>
        public UnityEvent<NetworkServer> onServerStopped = new UnityEvent<NetworkServer>();
        
        /// <summary>
        /// Fired when a client's connection to the server has been accepted.
        /// </summary>
        public UnityEvent<ulong> onClientConnected = new UnityEvent<ulong>();

        /// <summary>
        /// Fired when a client's connection is denied
        /// </summary>
        public UnityEvent<ulong> onClientDisconnected = new UnityEvent<ulong>();
        
        /// <summary>
        /// Mark as server context for packet management.
        /// </summary>
        protected override bool isServerContext => true;

        /// <summary>
        /// The transport the server is using.
        /// </summary>
        private Transport _transport;

        /// <summary>
        /// The connection verification hash
        /// </summary>
        private readonly ulong _verificationHash;

        /// <summary>
        /// Sender list for sending data to an individual client.
        /// Saves on allocations
        /// </summary>
        private readonly List<ulong> _singleSenderList = new List<ulong> { 0 };
        
        private class ClientInfo {
            public ulong clientID = 0;
            public bool clientApproved = false;
            public bool disconnectRequested = false;

            // Used for cancelling the timeout tasks.
            public CancellationTokenSource cancellationSource = new CancellationTokenSource();
            public Task requestTimeoutTask = null;

            public ClientInfo(ulong ID) {
                clientID = ID;
            }

            public void CancelTimeout() {
                cancellationSource.Cancel();
            }

            public void ResetCancellation() {
                cancellationSource = new CancellationTokenSource();
            }
        }

        private ConcurrentDictionary<ulong, ClientInfo> _clients = new ConcurrentDictionary<ulong, ClientInfo>();
        
        private readonly List<ulong> _allClientIDs = new List<ulong>();

        public NetworkServer(ulong verificationHash) {
            // Save the verification hash
            _verificationHash = verificationHash;
            
            // Register the common handlers. These are required across *all* servers.
            RegisterInternalPackets();
        }

        #region Server Control

        /// <summary>
        /// Start the server.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if a server is already running.</exception>
        public void Start(Transport transport) {
            // Check that the transport isn't running, or if a network server already exists.
            if (transport.serverRunning || instance != null)
                throw new NotSupportedException("A server is already running!");
            
            // Save transport and start it
            _transport = transport;
            _transport.ServerStart();
            
            // Hook transport events
            HookTransport();
            
            // Save as the current server and fire start events
            instance = this;
            onServerStarted?.Invoke(this);
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if a server is not running.</exception>
        public void Stop() {
            if (_transport == null || !_transport.serverRunning)
                throw new NotSupportedException("A server is not running!");
            if (instance != this)
                throw new NotSupportedException("This instance should not be running a server!");
            PerformStop();
        }

        private void PerformStop() {
            _transport.ServerShutdown();
            UnhookTransport();
            instance = null;
            onServerStopped?.Invoke(this);
        }

        #endregion
        
        #region Sending to Clients
        
        /// <summary>
        /// Send a packet to a specific client.
        /// </summary>
        /// <param name="clientID">Target client</param>
        /// <param name="packet">The packet to send</param>
        /// <param name="channelID">The channel to send the message with.</param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="InvalidOperationException">Thrown when the packet is not marked ClientPacket.</exception>
        public void Send<T>(ulong clientID, T packet, int channelID = DefaultChannels.Reliable) where T : ISerializable {
            // We reuse the list for server sending
            _singleSenderList[0] = clientID;
            Send(_singleSenderList, packet, channelID);
        }

        /// <summary>
        /// Broadcast a packet to a list of clients.
        /// </summary>
        /// <param name="clients">Clients to receive the packet.</param>
        /// <param name="packet">The packet to broadcast.</param>
        /// <param name="channelID">The channel to send the message with.</param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="InvalidOperationException">Thrown when the packet is not marked ClientPacket.</exception>
        public void Send<T>(List<ulong> clients, T packet, int channelID = DefaultChannels.Reliable) where T : ISerializable {
            if (!PacketUtils.IsClientPacket<T>())
                throw new InvalidOperationException("Cannot send a packet to a client that isn't marked as a client packet!");
            
            // Write data
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedUInt32(PacketUtils.GetID<T>());
                writer.WritePackedObject(packet);
                _transport.ServerSend(clients, writer.ToArraySegment(), channelID);
            }
        }

        /// <summary>
        /// Broadcast a packet to every connected client.
        /// </summary>
        /// <param name="packet">The packet to broadcast.</param>
        /// <param name="channelID">The channel to send the message with.</param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="InvalidOperationException">Thrown when the packet is not marked ClientPacket.</exception>
        public void SendToAll<T>(T packet, int channelID = DefaultChannels.Reliable) where T : ISerializable {
            Send(_allClientIDs, packet, channelID);
        }
        
        #endregion
        
        #region Client Management

        /// <summary>
        /// Disconnect a client from the server.
        /// </summary>
        /// <param name="clientID">The client to be disconnected.</param>
        /// <param name="disconnectReason">The client disconnect reason. Will be provided to the client on disconnect.</param>
        public void Disconnect(ulong clientID, string disconnectReason) {
            // Don't send multiple disconnects
            var client = _clients[clientID];
            if (client.disconnectRequested)
                return;
            client.disconnectRequested = true;

            // Send disconect packet
            var disconnect = new ClientDisconnectPacket {disconnectReason = disconnectReason};
            Send(clientID, disconnect);
            
            // Start a timeout task
            client.ResetCancellation();
            Task.Run(() => {
                         // Stop task if cancellation was requested before we started waiting.
                         client.cancellationSource.Token.ThrowIfCancellationRequested();
                         Thread.Sleep(20000); // The client has 20 seconds to connect.
                         
                         // Throw if cancellation was requested. This would happen if the countdown had started, but the client had connected.
                         client.cancellationSource.Token.ThrowIfCancellationRequested();
                         
                         // Get rid of the client. Its bad.
                         Debug.Log("Disconnecting client that hasn't disconnected after sending a disconnect confirmation packet 20 seconds ago.");
                         _transport.ServerDisconnect(clientID);
                     }, client.cancellationSource.Token);
        }

        #endregion
        
        #region Packet Handlers (Internal Protocol)

        /// <summary>
        /// Register internal server packets.
        /// </summary>
        private void RegisterInternalPackets() {
            RegisterPacketHandler<ConnectionRequestPacket>(HandleConnectionRequest);
        }

        /// <summary>
        /// Handle a client connection request.
        /// </summary>
        /// <param name="clientID">The client requesting connection</param>
        /// <param name="packet">The request packet.</param>
        private void HandleConnectionRequest(ulong clientID, ConnectionRequestPacket packet) {
            // Client has requested connection. We can stop the timeout thread.
            _clients[clientID].CancelTimeout();
            
            // Check verification hash.
            if (packet.verificationHash != _verificationHash) {
                Disconnect(clientID, "Client version does not match the server's.");
            }

            // TODO: Add a delegate that will use the extra data sent with the request to approve or deny the connection.
            //  For now, we just accept if the verification hash matches.
            
            // Send the acceptance packet
            var accept = new ConnectionApprovedPacket {localClientID = clientID};
            Send(clientID, accept);
            
            // Mark as approved in clients list
            _clients[clientID].clientApproved = true;
            
            // Fire the connected event.
            onClientConnected?.Invoke(clientID);
        }

        #endregion
        
        #region Underlying Implementation (Transport interactions)
        
        /// <summary>
        /// Hooks transport events for the server.
        /// </summary>
        private void HookTransport() {
            _transport.onServerDataReceived.AddListener(HandlePacket);
            _transport.onServerConnected.AddListener(ClientConnectionHandler);
            _transport.onServerDisconnected.AddListener(ClientDisconnectionHandler);
        }

        /// <summary>
        /// Detaches from the transport for reuse.
        /// </summary>
        private void UnhookTransport() {
            _transport.onServerDataReceived.RemoveListener(HandlePacket);
            _transport.onServerConnected.RemoveListener(ClientConnectionHandler);
            _transport.onServerDisconnected.RemoveListener(ClientDisconnectionHandler);
        }
        
        private void ClientConnectionHandler(ulong clientID) {
            // Add client to the clients list
            if (!_clients.TryAdd(clientID, new ClientInfo(clientID)))
                throw new Exception("Failed to add client to clients list!!");
            _allClientIDs.Add(clientID);

            var client = _clients[clientID];
            
            // Start a timeout task
            Task.Run(() => {
                         // Stop task if cancellation was requested before we started waiting.
                         client.cancellationSource.Token.ThrowIfCancellationRequested();
                         Thread.Sleep(20000); // The client has 20 seconds to connect.
                         
                         // Throw if cancellation was requested. This would happen if the countdown had started, but the client had connected.
                         client.cancellationSource.Token.ThrowIfCancellationRequested();
                         
                         // Get rid of the client. Its bad.
                         Debug.Log("Disconnecting client that has not sent a connection request after 20 seconds.");
                         
                         // We do this nicely so if they do get this packet, they know why they were disconected.
                         Disconnect(clientID, "Connection request time out. Please try to connect again.");
                     }, client.cancellationSource.Token);
        }

        private void ClientDisconnectionHandler(ulong clientID) {
            // Get the client and cancel the timeout task (either they have honoured it, or it has done its job).
            _clients[clientID].CancelTimeout();

            // TODO: Remove *any* references to this client ID now! Once the pooling system is added, this ID can be reused.
            
            // Remove from clients list
            if (!_clients.TryRemove(clientID, out _))
                throw new Exception("Failed to remove client from the list!!");
            _allClientIDs.Remove(clientID);
            
            // Fire the event.
            onClientDisconnected?.Invoke(clientID);
        }
        
        #endregion
    }
}