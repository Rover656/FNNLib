using System;
using System.Collections.Generic;
using System.IO;
using FNNLib.Messaging;
using FNNLib.Messaging.Internal;
using FNNLib.Serialization;
using FNNLib.Transports;

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
        public static NetworkServer Instance = null;

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
        public event NetworkServerEventHandler OnServerStarted;
        
        /// <summary>
        /// Server stopped event. Raised after the server is closed and clients are disconnected.
        /// </summary>
        public event NetworkServerEventHandler OnServerStopped;
        
        /// <summary>
        /// Mark as server context for packet management.
        /// </summary>
        protected override bool isServerContext => true;

        public class Client {
            public int clientID { get; internal set; }
            public uint currentScene { get; internal set; }
        }

        private readonly Dictionary<int, int> _clients;
        
        public NetworkServer() {
            // Register the common handlers. These are required across *all* servers.
            RegisterInternalPackets();
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

        private void HookTransport() {
            Transport.currentTransport.onServerDataReceived.AddListener(HandlePacket);
        }

        private void UnHookTransport() {
            Transport.currentTransport.onServerDataReceived.RemoveListener(HandlePacket);
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

        #region Packet Handlers

        private void RegisterInternalPackets() {
            RegisterPacketHandler<ConnectionRequestPacket>(HandleConnectionRequest);
        }

        private void HandleConnectionRequest(int clientID, ConnectionRequestPacket packet) {
            // TODO: When a client connects, immediately start a thread that disconnects them after X seconds of inactivity to prevent clients connecting and not requesting connection.
            // TODO: Allow the developer to use the custom data sent in the packet to decide if the connection is accepted.
            //  For now I will just send an accept packet
            var accept = new ConnectionApprovedPacket {localClientID = clientID};
            Send(clientID, accept);
        }

        #endregion
    }
}