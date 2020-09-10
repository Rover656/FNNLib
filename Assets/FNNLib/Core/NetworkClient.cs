using System;
using FNNLib.Messaging;
using FNNLib.Messaging.Internal;
using FNNLib.Serialization;
using FNNLib.Transports;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Experimental.AI;

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

        public event ConnectionApprovedDelegate OnConnectionApproved;

        /// <summary>
        /// Mark as client context for packet management.
        /// </summary>
        protected override bool isServerContext => false;

        public NetworkClient(int protocolVersion) {
            // TODO: Protocol version as part of the connection process...
        }

        #region Client Control

        private byte[] _connectionRequestData;

        public void Connect(string hostname, byte[] connectionRequestData = null) {
            Instance = this;
            Transport.currentTransport.StartClient(hostname);
            // TODO: Hook connect, disconnect events etc. and hook up the connection request system.
            RegisterInternalPackets();

            // Hook events
            HookEvents();

            // Save the data for use in the connection request phase
            _connectionRequestData = connectionRequestData;
        }

        public void BeginHost() {
            // TODO: Begin a virtual client
        }

        public void Disconnect() {
            Transport.currentTransport.StopClient();
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
            // Connection approval
            RegisterPacketHandler<ConnectionApprovedPacket>((serverID, packet) => {
                                                                localClientID = packet.localClientID;
                                                                OnConnectionApproved?.Invoke(packet.localClientID);
                                                            });
        }

        #endregion
    }
}