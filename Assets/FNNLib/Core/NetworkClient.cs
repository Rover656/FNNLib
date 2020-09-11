using System;
using System.Collections.Concurrent;
using FNNLib.Messaging;
using FNNLib.Messaging.Internal;
using FNNLib.Serialization;
using FNNLib.Transports;
using UnityEngine;
using UnityEngine.Events;

namespace FNNLib.Core {
    public class NetworkClient : PacketHandler {
        /// <summary>
        /// The current NetworkClient
        /// </summary>
        public static NetworkClient Instance;

        /// <summary>
        /// This client's ID on the server.
        /// </summary>
        public int localClientID { get; private set; }

        public UnityEvent OnConnectionApproved = new UnityEvent();
        
        public UnityEvent<string> OnDisconected = new UnityEvent<string>();

        /// <summary>
        /// Mark as client context for packet management.
        /// </summary>
        protected override bool isServerContext => false;

        public NetworkClient(int protocolVersion) {
            // TODO: Protocol version as part of the connection process...
        }

        #region Client Control

        private byte[] _connectionRequestData;

        /// <summary>
        /// Connect the client to a server.
        /// </summary>
        /// <param name="hostname">The server hostname</param>
        /// <param name="connectionRequestData">Additional connection data used by the server approval phase.</param>
        public void Connect(string hostname, byte[] connectionRequestData = null) {
            // TODO: Security checks.
            Instance = this;
            Transport.currentTransport.StartClient(hostname);
            
            // Register the internal protocol
            RegisterInternalPackets();

            // Hook events
            HookEvents();

            // Save the data for use in the connection request phase
            _connectionRequestData = connectionRequestData;
        }

        public void BeginHost() {
            // TODO: Begin a virtual client. This will stop any outgoing packets to the server.
        }

        public void Disconnect() {
            Transport.currentTransport.StopClient();
            OnDisconected.Invoke(null);
        }

        private void ClientConnected() {
            Debug.Log("Connected to server! Sending connection request.");

            // Send connection request
            var request = new ConnectionRequestPacket {connectionData = _connectionRequestData};
            Send(request);
        }

        private void HookEvents() {
            Transport.currentTransport.onClientConnected.AddListener(ClientConnected);
            Transport.currentTransport.onClientDataReceived.AddListener(HandlePacket);
        }

        private void UnhookEvents() {
            Transport.currentTransport.onClientConnected.RemoveListener(ClientConnected);
            Transport.currentTransport.onClientDataReceived.RemoveListener(HandlePacket);
        }

        /// <summary>
        /// Passthrough which uses the server client ID.
        /// </summary>
        /// <param name="data">The data packet.</param>
        private void HandlePacket(ArraySegment<byte> data) {
            HandlePacket(Transport.currentTransport.serverClientID, data);
        }

        #endregion

        #region Sending to Server

        /// <summary>
        /// Send a packet to the server.
        /// </summary>
        /// <param name="packet"></param>
        /// <typeparam name="T"></typeparam>
        /// <exception cref="InvalidOperationException">Thrown when the packet is not marked ServerPacket.</exception>
        public void Send<T>(T packet) where T : IPacket, new() {
            if (!PacketUtils.IsServerPacket<T>())
                throw new InvalidOperationException("Attempted to send non-server packet to server!");

            // Write data
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WriteInt32(PacketUtils.GetID<T>());
                packet.Serialize(writer);
                Transport.currentTransport.ClientSend(writer.ToArraySegment());
            }
        }

        #endregion

        #region Packet Handlers

        /// <summary>
        /// Registers the internal packets for the protocol
        /// </summary>
        private void RegisterInternalPackets() {
            RegisterPacketHandler<ConnectionApprovedPacket>(ConnectionApprovedHandler);
            RegisterPacketHandler<ClientDisconnectPacket>(ClientDisconnectHandler);
        }

        private void ConnectionApprovedHandler(int sender, ConnectionApprovedPacket packet) {
            localClientID = packet.localClientID;
            OnConnectionApproved?.Invoke();
        }

        private void ClientDisconnectHandler(int sender, ClientDisconnectPacket packet) {
            // Disconnect from the server, then fire the disconnect event with our disconnection reason.
            Transport.currentTransport.StopClient();
            OnDisconected?.Invoke(packet.disconnectReason);
        }

        #endregion
    }
}