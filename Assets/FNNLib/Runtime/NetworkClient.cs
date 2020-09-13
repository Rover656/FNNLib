using System;
using FNNLib.Messaging;
using FNNLib.Messaging.Internal;
using FNNLib.Serialization;
using FNNLib.Transports;
using UnityEngine;
using UnityEngine.Events;

namespace FNNLib {
    public class NetworkClient : PacketHandler {
        /// <summary>
        /// The current NetworkClient
        /// </summary>
        public static NetworkClient instance;

        /// <summary>
        /// This client's ID on the server.
        /// </summary>
        public ulong localClientID { get; private set; }

        /// <summary>
        /// Fired when the client is approved by the server.
        /// </summary>
        public UnityEvent onConnected = new UnityEvent();
        
        /// <summary>
        /// Fired when the client is disconnected from the server, either by choice or by the server.
        /// If disconnected by choice, reason is null.
        /// Comes with a reason parameter.
        /// </summary>
        public UnityEvent<string> onDisconnected = new UnityEvent<string>();

        /// <summary>
        /// Mark as client context for packet management.
        /// </summary>
        protected override bool isServerContext => false;

        /// <summary>
        /// The transport the client is using.
        /// </summary>
        private Transport _transport;

        /// <summary>
        /// The protocol version of the client.
        /// </summary>
        private readonly int _protocolVersion;

        /// <summary>
        /// If running in host mode, we don't send packets to the server.
        /// Systems are supposed to account for host mode (for example RPC calls will directly route instead of going via packets).
        /// </summary>
        private bool _hostMode;

        /// <summary>
        /// Creates a network client with the given protocol version.
        /// </summary>
        /// <param name="protocolVersion">Client protocol version.</param>
        public NetworkClient(int protocolVersion) {
            // Set protocol version
            _protocolVersion = protocolVersion;
            
            // Register internal protocol packets
            RegisterInternalPackets();
        }

        #region Client Control

        /// <summary>
        /// The data to be sent with the connection request.
        /// </summary>
        private byte[] _connectionRequestData;

        /// <summary>
        /// Connect the client to a server.
        /// </summary>
        /// <param name="hostname">The server hostname</param>
        /// <param name="connectionRequestData">Additional connection data used by the server approval phase.</param>
        public void Connect(Transport transport, string hostname, byte[] connectionRequestData = null) {
            if (instance != null || transport.clientConnected)
                throw new NotSupportedException("A client is already running!");
            
            // Save the transport and start
            _transport = transport;
            _transport.ClientConnect(hostname);
            instance = this;
            _hostMode = false;

            // Hook events
            HookTransport();

            // Save the data for use in the connection request phase
            _connectionRequestData = connectionRequestData;
        }

        public void ConnectVirtual() {
            // TODO: Begin a virtual client. This will stop any outgoing packets to the server.
        }

        public void Disconnect() {
            if (instance == null)
                throw new NotSupportedException("The client is not running!");
            if (instance != this)
                throw new NotSupportedException("A client is running, however this is not the running client!");
            
            // Stop the client.
            _transport.ClientDisconnect();
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
            
            // In host mode, we don't send packets to the server.
            if (_hostMode)
                return;

            // Write data
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WriteInt32(PacketUtils.GetID<T>());
                packet.Serialize(writer);
                _transport.ClientSend(writer.ToArraySegment());
            }
        }

        #endregion

        #region Packet Handlers (Internal Protocol)

        /// <summary>
        /// Registers the internal packets for the protocol
        /// </summary>
        private void RegisterInternalPackets() {
            RegisterPacketHandler<ConnectionApprovedPacket>(ConnectionApprovedHandler);
            RegisterPacketHandler<ClientDisconnectPacket>(ClientDisconnectHandler);
        }

        /// <summary>
        /// Once the connection is approved, the game can start running its logic.
        /// </summary>
        /// <param name="sender">The sender (the server in this instance).</param>
        /// <param name="packet">The connection approval packet.</param>
        private void ConnectionApprovedHandler(ulong sender, ConnectionApprovedPacket packet) {
            localClientID = packet.localClientID;
            onConnected?.Invoke();
        }

        /// <summary>
        /// When the server disconnects us, we will save the reason for the event and stop the client.
        /// </summary>
        /// <param name="sender">The server</param>
        /// <param name="packet">The disconnection packet.</param>
        private void ClientDisconnectHandler(ulong sender, ClientDisconnectPacket packet) {
            // Disconnect from the server, storing the disconnect reason for the disconnect callback.
            _disconnectReason = packet.disconnectReason;
            _transport.ClientDisconnect();
        }

        #endregion
        
        #region Underlying Implementation (Transport interactions)

        /// <summary>
        /// The disconnect reason.
        /// This is reset when the client is created (in HookEvents).
        /// Then this is set if a reason is received by the disconnect handler
        /// </summary>
        private string _disconnectReason;
        
        /// <summary>
        /// Attach to the transport ready for use.
        /// </summary>
        private void HookTransport() {
            _transport.onClientDataReceived.AddListener(HandlePacket);
            _transport.onClientConnected.AddListener(ClientConnected);
            _transport.onClientDisconnected.AddListener(ClientDisconnected);
            
            // Also reset the disconnect reason because this is a new connection.
            _disconnectReason = null;
        }

        /// <summary>
        /// Disconnect from the transport so it can be reused
        /// </summary>
        private void UnhookTransport() {
            _transport.onClientDataReceived.RemoveListener(HandlePacket);
            _transport.onClientConnected.RemoveListener(ClientConnected);
            _transport.onClientDisconnected.RemoveListener(ClientDisconnected);
        }

        /// <summary>
        /// Passthrough which uses the server client ID.
        /// </summary>
        /// <param name="data">The data packet.</param>
        private void HandlePacket(ArraySegment<byte> data, int channelID) {
            HandlePacket(0, data, channelID);
        }
        
        /// <summary>
        /// Client connection actions.
        /// </summary>
        private void ClientConnected() {
            Debug.Log("Connected to server! Sending connection request.");

            // Send connection request
            var request = new ConnectionRequestPacket {connectionData = _connectionRequestData, protocolVersion = _protocolVersion};
            Send(request);
        }

        /// <summary>
        /// Client disconnection actions.
        /// </summary>
        private void ClientDisconnected() {
            // Fire disconnect
            onDisconnected?.Invoke(_disconnectReason);
            
            // Unhook from the transport.
            UnhookTransport();
        }
        
        #endregion
    }
}