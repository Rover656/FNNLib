using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FNNLib.Messaging;
using FNNLib.Messaging.Internal;
using FNNLib.Serialization;
using FNNLib.Transports;
using UnityEngine;
using UnityEngine.Events;

namespace FNNLib.Core {
    public delegate void NetworkServerEventHandler(NetworkServer server);
    
    /// <summary>
    /// The NetworkServer class manages sending data to the clients.
    /// This is normally controlled by the NetworkManager and most games won't access this at all.
    /// This is only accessed if you wish to register custom packet types.
    /// </summary>
    public class NetworkServer : PacketHandler {
        /// <summary>
        /// The currently running server, if any.
        /// Check if a server is running with the IsServerRunning static property.
        /// </summary>
        public static NetworkServer Instance;

        /// <summary>
        /// Whether or not a server is running.
        /// </summary>
        public static bool IsServerRunning => Instance != null && Instance.running;

        /// <summary>
        /// Whether or not this server is running.
        /// </summary>
        public bool running => Transport.currentTransport.serverRunning && Instance == this;

        /// <summary>
        /// Server started event. Raised just after the server is started.
        /// </summary>
        public UnityEvent<NetworkServer> OnServerStarted = new UnityEvent<NetworkServer>();
        
        /// <summary>
        /// Server stopped event. Raised after the server is closed and clients are disconnected.
        /// </summary>
        public UnityEvent<NetworkServer> OnServerStopped = new UnityEvent<NetworkServer>();
        
        /// <summary>
        /// Fired when a client has connected to the server.
        /// </summary>
        public UnityEvent<int> OnClientConnected = new UnityEvent<int>();
        
        /// <summary>
        /// Fired when a client's connection is approved
        /// </summary>
        public UnityEvent<int> OnClientApproved = new UnityEvent<int>();
        
        /// <summary>
        /// Fired when a client's connection is denied
        /// </summary>
        public UnityEvent<int> OnClientDisconnected = new UnityEvent<int>();
        
        /// <summary>
        /// Mark as server context for packet management.
        /// </summary>
        protected override bool isServerContext => true;

        /// <summary>
        /// The server protocol version.
        /// This stops incompatible client-server interactions.
        /// </summary>
        private readonly int _protocolVersion;
        
        private class ClientInfo {
            public int clientID = 0;
            public bool clientApproved = false;
            public bool disconnectRequested = false;

            // Used for cancelling the timeout tasks.
            public CancellationTokenSource cancellationSource = new CancellationTokenSource();
            public Task requestTimeoutTask = null;

            public ClientInfo(int ID) {
                clientID = ID;
            }

            public void CancelTimeout() {
                cancellationSource.Cancel();
            }

            public void ResetCancellation() {
                cancellationSource = new CancellationTokenSource();
            }
        }
        
        private ConcurrentDictionary<int, ClientInfo> _clients = new ConcurrentDictionary<int, ClientInfo>();

        public NetworkServer(int protocolVersion) {
            // Register the common handlers. These are required across *all* servers.
            RegisterInternalPackets();
            
            // Save the protocol version
            _protocolVersion = protocolVersion;
        }

        #region Server Control

        /// <summary>
        /// Start the server.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if a server is already running.</exception>
        public void Start() {
            if (Transport.currentTransport.serverRunning || Instance != null)
                throw new NotSupportedException("A server is already running!");
            Transport.currentTransport.StartServer();
            HookTransport();
            Instance = this;
            OnServerStarted?.Invoke(this);
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if a server is not running.</exception>
        public void Stop() {
            if (!Transport.currentTransport.serverRunning)
                throw new NotSupportedException("A server is not running!");
            if (Instance != this)
                throw new NotSupportedException("This instance should not be running a server!");
            PerformStop();
        }

        private void PerformStop() {
            Transport.currentTransport.StopServer();
            UnHookTransport();
            Instance = null;
            OnServerStopped?.Invoke(this);
        }

        #endregion
        
        #region Sending to Clients
        
        /// <summary>
        /// Send a packet to a specific client.
        /// </summary>
        /// <param name="clientID">Target client</param>
        /// <param name="packet">The packet to send</param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="InvalidOperationException">Thrown when the packet is not marked ClientPacket.</exception>
        public void Send<T>(int clientID, T packet) where T : IPacket {
            if (!PacketUtils.IsClientPacket<T>())
                throw new InvalidOperationException("Cannot send a packet to a client that isn't marked as a client packet!");
            
            // Write data
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WriteInt32(PacketUtils.GetID<T>());
                packet.Serialize(writer);
                Transport.currentTransport.ServerSend(clientID, writer.ToArraySegment());
            }
        }

        /// <summary>
        /// Broadcast a packet to a list of clients.
        /// </summary>
        /// <param name="clients">Clients to receive the packet.</param>
        /// <param name="packet">The packet to broadcast.</param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="InvalidOperationException">Thrown when the packet is not marked ClientPacket.</exception>
        public void Send<T>(IEnumerable<int> clients, T packet) where T : IPacket {
            if (!PacketUtils.IsClientPacket<T>())
                throw new InvalidOperationException("Cannot send a packet to a client that isn't marked as a client packet!");
            
            // Write data
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WriteInt32(PacketUtils.GetID<T>());
                packet.Serialize(writer);
                foreach (var client in clients)
                    Transport.currentTransport.ServerSend(client, writer.ToArraySegment());
            }
        }
        
        #endregion
        
        #region Client Management

        /// <summary>
        /// Disconnect a client from the server.
        /// </summary>
        /// <param name="clientID">The client to be disconnected.</param>
        public void Disconnect(int clientID, string disconnectReason) {
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
                         Debug.Log("Disconnecting client that hasn't disconnected after sending a disconnect packet 20 seconds ago.");
                         Transport.currentTransport.ServerDisconnectClient(clientID);
                     }, client.cancellationSource.Token);
        }

        #endregion
        
        #region Underlying Implementation (Transport interactions)
        
        /// <summary>
        /// Hooks transport events for the server.
        /// </summary>
        private void HookTransport() {
            var transport = Transport.currentTransport;
            transport.onServerDataReceived.AddListener(HandlePacket);
            transport.onServerConnected.AddListener(ClientConnectionHandler);
            transport.onServerDisconnected.AddListener(ClientDisconnectionHandler);
        }

        /// <summary>
        /// Detaches from the transport for reuse.
        /// </summary>
        private void UnHookTransport() {
            var transport = Transport.currentTransport;
            transport.onServerDataReceived.RemoveListener(HandlePacket);
            transport.onServerConnected.RemoveListener(ClientConnectionHandler);
            transport.onServerDisconnected.RemoveListener(ClientDisconnectionHandler);
        }
        
        private void ClientConnectionHandler(int clientID) {
            // Fire the on connected event.
            OnClientConnected?.Invoke(clientID);
            
            // Add client to the clients list
            if (!_clients.TryAdd(clientID, new ClientInfo(clientID)))
                throw new Exception("Failed to add client to clients list!!");

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
                         Disconnect(clientID, "Connection request timed out. Please try again.");
                     }, client.cancellationSource.Token);
        }

        private void ClientDisconnectionHandler(int clientID) {
            // Get the client and cancel the timeout task (either they have honoured it, or it has done its job).
            _clients[clientID].CancelTimeout();
            
            // Fire the event.
            OnClientDisconnected?.Invoke(clientID);

            // TODO: Remove *any* references to this client ID now! Once the pooling system is added, this ID can be reused.
            
            // Remove from clients list
            if (!_clients.TryRemove(clientID, out _))
                throw new Exception("Failed to remove client from the list!!");
        }
        
        #endregion

        #region Packet Handlers

        private void RegisterInternalPackets() {
            RegisterPacketHandler<ConnectionRequestPacket>(HandleConnectionRequest);
        }

        private void HandleConnectionRequest(int clientID, ConnectionRequestPacket packet) {
            // Client has requested connection. We can stop the timeout thread.
            _clients[clientID].CancelTimeout();
            
            // Check protocol version
            if (packet.protocolVersion < _protocolVersion) {
                Disconnect(clientID, "Client is outdated.");
            } else if (packet.protocolVersion > _protocolVersion) {
                Disconnect(clientID, "Client is newer than the server.");
            }

            // TODO: Add a delegate that will use the extra data sent with the request to approve or deny the connection.
            //  For now, we just accept if the protocol version matches.
            
            // Send the acceptance packet
            var accept = new ConnectionApprovedPacket {localClientID = clientID};
            Send(clientID, accept);
            
            // Mark as approved in clients list
            _clients[clientID].clientApproved = true;
            
            // Fire the approval event
            OnClientApproved?.Invoke(clientID);
        }

        #endregion
    }
}