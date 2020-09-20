using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FNNLib.Config;
using FNNLib.Messaging;
using FNNLib.Messaging.Internal;
using FNNLib.RPC;
using FNNLib.SceneManagement;
using FNNLib.Serialization;
using FNNLib.Spawning;
using FNNLib.Transports;
using UnityEngine;
using UnityEngine.Events;

namespace FNNLib {
    public delegate void ServerPacketDelegate(ulong clientID, NetworkReader reader);

    public delegate void ClientPacketDelegate(NetworkReader reader);

    /// <summary>
    /// The network manager drives the NetworkClient and NetworkServer systems.
    ///
    /// TODO: Make extensible with Justin's NetworkLogic idea.
    /// </summary>
    [AddComponentMenu("Networking/Network Manager")]
    public class NetworkManager : MonoBehaviour {
        /// <summary>
        /// The local ID of the server
        /// </summary>
        public const ulong ServerLocalID = 0;

        /// <summary>
        /// The game's NetworkManager.
        /// </summary>
        public static NetworkManager instance;

        /// <summary>
        /// Whether or not the game should run clientside code.
        /// </summary>
        public bool isClient { get; private set; }

        /// <summary>
        /// Whether or not the game should run serverside code.
        /// </summary>
        public bool isServer { get; private set; }

        public bool isSinglePlayer { get; private set; }

        /// <summary>
        /// Whether or not the client is a virtual client.
        /// </summary>
        public bool isHost => isClient && isServer;

        /// <summary>
        /// The local client ID.
        /// </summary>
        public ulong localClientID => isServer ? ServerLocalID : _localClientID;

        private ulong _localClientID;

        public readonly Dictionary<ulong, NetworkedClient> connectedClients = new Dictionary<ulong, NetworkedClient>();

        public readonly List<NetworkedClient> connectedClientsList = new List<NetworkedClient>();

        /// <summary>
        /// Whether or not to move the manager to the DontDestroyOnLoad Scene.
        /// </summary>
        public bool dontDestroyOnLoad = true;

        /// <summary>
        /// Whether or not the application should run in the background while networking is running.
        /// Will be reset once the client/server is finished.
        /// </summary>
        public bool runInBackground = true;

        private bool _wasRunningInBackground;

        /// <summary>
        /// The network config.
        /// </summary>
        [HideInInspector] public NetworkConfig networkConfig;

        private void Awake() {
            // Instance manager
            if (instance != null && instance != this) {
                Destroy(gameObject);
            } else {
                instance = this;
                if (dontDestroyOnLoad)
                    DontDestroyOnLoad(this);
            }
        }

        private void OnDestroy() {
            if (isSinglePlayer)
                StopSinglePlayer();
            if (isHost)
                StopHost();
            if (isServer)
                StopServer();
            if (isClient)
                StopClient();
            if (instance == this)
                instance = null;
        }

        #region Server

        [HideInInspector] public UnityEvent<ulong> serverOnClientConnect = new UnityEvent<ulong>();

        [HideInInspector] public UnityEvent<ulong> serverOnClientDisconnect = new UnityEvent<ulong>();

        private List<ulong> _pendingClients = new List<ulong>();
        private List<ulong> _clientIDs = new List<ulong>();

        /// <summary>
        /// Starts the manager in server mode.
        /// </summary>
        public void StartServer() {
            // Check that the transport is set.
            if (networkConfig.transport == null)
                throw new InvalidOperationException("The NetworkManager must be provided with a transport!");
            if (isSinglePlayer)
                throw new NotSupportedException("The network manager is already running in single player mode!");
            if (isHost)
                throw new NotSupportedException("The network manager is already running in host mode!");
            if (isClient)
                throw new NotSupportedException("The network manager is already running in client mode!");
            if (isServer)
                throw new NotSupportedException("A server is already running!");

            // Init
            Init();

            // Start server.
            networkConfig.transport.ServerStart();
            isServer = true;

            // Server fps fix
            ConfigureServerFramerate();

            // Set starting scene.
            SpawnManager.ServerSpawnSceneObjects(NetworkSceneManager.RegisterInitialScene());
        }

        /// <summary>
        /// Stop a running server.
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        public void StopServer() {
            if (isSinglePlayer)
                throw new
                    NotSupportedException("The network manager is running in single player mode! Use StopSinglePlayer().");
            if (isHost)
                throw new NotSupportedException("The network manager is running in host mode! Use StopHost() instead.");
            if (isClient)
                throw new
                    NotSupportedException("The network manager is running in client mode! Use StopClient() instead.");
            if (!isServer)
                throw new NotSupportedException("A server is not running!");

            // Shutdown
            Shutdown();

            // Stop server
            networkConfig.transport
                         .ServerShutdown(); // TODO: Thread safe way for transport to send events to the manager.
            isServer = false;
        }

        /// <summary>
        /// Disconnect a client with a reason
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="disconnectReason"></param>
        public void ServerDisconnect(ulong clientID, string disconnectReason) {
            // Send disconnect packet
            ServerSend(clientID, new ClientDisconnectPacket {disconnectReason = disconnectReason});

            // Start timeout
            StartCoroutine(ServerClientDisconnectTimeout(clientID));
        }

        /// <summary>
        /// Disconnect a client without a reason.
        /// </summary>
        /// <param name="clientID">Client to disconnect.</param>
        public void ServerForceDisconnect(ulong clientID) {
            networkConfig.transport.ServerDisconnect(clientID);
        }

        /// <summary>
        /// Send a packet to the given client.
        /// </summary>
        /// <param name="clientID">The client to send to.</param>
        /// <param name="packet">The packet to send</param>
        /// <param name="channel">The channel to send with</param>
        /// <typeparam name="TPacket">The packet type</typeparam>
        public void ServerSend<TPacket>(ulong clientID, TPacket packet, int channel = DefaultChannels.Reliable)
            where TPacket : ISerializable, new() {
            // Verify the packet is for the client
            if (!PacketUtils.IsClientPacket<TPacket>())
                throw new
                    InvalidOperationException("Cannot send a packet to a client that isn't marked as a client packet!");

            // Write and send
            using (var writer = NetworkWriterPool.GetWriter()) {
                // Write packet
                WritePacket(packet, writer);

                // Send
                networkConfig.transport.ServerSend(clientID, writer.ToArraySegment(), channel);
            }
        }

        /// <summary>
        /// Send a packet to all provided clients.
        /// </summary>
        /// <param name="clientIDs">The clients to send to.</param>
        /// <param name="packet">The packet to send</param>
        /// <param name="channel">The channel to send with</param>
        /// <typeparam name="TPacket">The packet type</typeparam>
        public void ServerSend<TPacket>(List<ulong> clientIDs, TPacket packet, int channel = DefaultChannels.Reliable)
            where TPacket : ISerializable, new() {
            // Verify the packet is for the client
            if (!PacketUtils.IsClientPacket<TPacket>())
                throw new
                    InvalidOperationException("Cannot send a packet to a client that isn't marked as a client packet!");

            // Write and send
            using (var writer = NetworkWriterPool.GetWriter()) {
                // Write packet
                WritePacket(packet, writer);

                // Send
                networkConfig.transport.ServerSend(clientIDs, writer.ToArraySegment(), channel);
            }
        }

        /// <summary>
        /// Send a packet to all provided clients excluding one.
        /// </summary>
        /// <param name="clientIDs">The clients to send to.</param>
        /// <param name="excludedClientID">The client to exclude.</param>
        /// <param name="packet">The packet to send</param>
        /// <param name="channel">The channel to send with</param>
        /// <typeparam name="TPacket">The packet type</typeparam>
        public void ServerSendExcluding<TPacket>(List<ulong> clientIDs, ulong excludedClientID, TPacket packet,
                                                 int channel = DefaultChannels.Reliable)
            where TPacket : ISerializable, new() {
            // Verify the packet is for the client
            if (!PacketUtils.IsClientPacket<TPacket>())
                throw new
                    InvalidOperationException("Cannot send a packet to a client that isn't marked as a client packet!");

            // Write and send
            using (var writer = NetworkWriterPool.GetWriter()) {
                // Write packet
                WritePacket(packet, writer);

                // Send
                networkConfig.transport.ServerSend(clientIDs, writer.ToArraySegment(), channel, excludedClientID);
            }
        }

        /// <summary>
        /// Send a packet to all connected clients.
        /// </summary>
        /// <param name="packet">The packet to send</param>
        /// <param name="channel">The channel to send with</param>
        /// <typeparam name="TPacket">The packet type</typeparam>
        public void ServerSendToAll<TPacket>(TPacket packet, int channel = DefaultChannels.Reliable)
            where TPacket : ISerializable, new() {
            // Verify the packet is for the client
            if (!PacketUtils.IsClientPacket<TPacket>())
                throw new
                    InvalidOperationException("Cannot send a packet to a client that isn't marked as a client packet!");

            // Write and send
            using (var writer = NetworkWriterPool.GetWriter()) {
                // Write packet
                WritePacket(packet, writer);

                // Send
                networkConfig.transport.ServerSend(_clientIDs, writer.ToArraySegment(), channel);
            }
        }

        /// <summary>
        /// Send a packet to all connect clients excluding one.
        /// </summary>
        /// <param name="excludedClientID">The client to exclude.</param>
        /// <param name="packet">The packet to send</param>
        /// <param name="channel">The channel to send with</param>
        /// <typeparam name="TPacket">The packet type</typeparam>
        public void ServerSendToAllExcluding<TPacket>(ulong excludedClientID, TPacket packet,
                                                      int channel = DefaultChannels.Reliable)
            where TPacket : ISerializable, new() {
            // Verify the packet is for the client
            if (!PacketUtils.IsClientPacket<TPacket>())
                throw new
                    InvalidOperationException("Cannot send a packet to a client that isn't marked as a client packet!");

            // Write and send
            using (var writer = NetworkWriterPool.GetWriter()) {
                // Write packet
                WritePacket(packet, writer);

                // Send
                networkConfig.transport.ServerSend(_clientIDs, writer.ToArraySegment(), channel, excludedClientID);
            }
        }

        private void ServerOnClientConnect(ulong clientID) {
            // Add to the pending clients and begin connection request timeout
            _pendingClients.Add(clientID);

            // Start disconnect coroutine
            StartCoroutine(ClientConnectionTimeout(clientID));
        }

        private void ServerOnDataReceived(ulong clientID, ArraySegment<byte> data, int channel) {
            HandlePacket(clientID, data, channel);
        }

        private void ServerOnClientDisconnect(ulong clientID) {
            if (_pendingClients.Contains(clientID))
                _pendingClients.Remove(clientID);

            if (connectedClients.ContainsKey(clientID)) {
                // Destroy owned objects
                for (var i = connectedClients[clientID].ownedObjects.Count - 1; i > -1; i--) {
                    SpawnManager.OnDestroy(connectedClients[clientID].ownedObjects[i], true);
                }

                // Destroy player object
                if (connectedClients[clientID].playerObject > 0)
                    SpawnManager.OnDestroy(connectedClients[clientID].playerObject, true);

                connectedClientsList.Remove(connectedClients[clientID]);
                connectedClients.Remove(clientID);
            }

            // Fire event
            serverOnClientDisconnect?.Invoke(clientID);
        }

        /// <summary>
        /// Ensures that the client responds with a connection request in a timely fashion.
        /// </summary>
        /// <param name="clientID"></param>
        /// <returns></returns>
        private IEnumerator ClientConnectionTimeout(ulong clientID) {
            var timeBegan = Time.unscaledTime;
            while (Time.unscaledTime - timeBegan < networkConfig.connectionRequestTimeout &&
                   _pendingClients.Contains(clientID))
                yield return null;

            if (_pendingClients.Contains(clientID) && !connectedClients.ContainsKey(clientID)) {
                Debug.Log("Disconnecting client " + clientID +
                          ". Did not send a connection request in a timely fashion");
                networkConfig.transport.ServerDisconnect(clientID);
            }
        }

        /// <summary>
        /// Ensures that client honours a disconnect request within a given timeframe.
        /// Stops clients clinging on by ignoring the request.
        /// </summary>
        /// <param name="clientID"></param>
        /// <returns></returns>
        private IEnumerator ServerClientDisconnectTimeout(ulong clientID) {
            var timeBegan = Time.unscaledTime;
            while (Time.unscaledTime - timeBegan < networkConfig.disconnectRequestTimeout &&
                   (_pendingClients.Contains(clientID) || connectedClients.ContainsKey(clientID)))
                yield return null;

            if (_pendingClients.Contains(clientID) || connectedClients.ContainsKey(clientID)) {
                Debug.Log("Disconnecting client " + clientID +
                          ". Did not send a connection request in a timely fashion");
                networkConfig.transport.ServerDisconnect(clientID);
            }
        }

        private void ServerHandleConnectionRequest(ulong clientID, ConnectionRequestPacket packet) {
            // Ignore extra approvals.
            if (connectedClients.ContainsKey(clientID))
                return;

            // Remove from pending clients list
            if (_pendingClients.Contains(clientID))
                _pendingClients.Remove(clientID);

            // Check hashes
            if (packet.verificationHash != networkConfig.GetHash()) {
                ServerDisconnect(clientID, "Client version does not match server!");
                return;
            }

            // TODO: Delegate to add extra acceptance logic.

            // Send approval
            ServerSend(clientID, new ConnectionApprovedPacket {localClientID = clientID});

            // Add client to connected clients
            connectedClients.Add(clientID, new NetworkedClient {
                                                                   clientID = clientID
                                                               });
            connectedClientsList.Add(connectedClients[clientID]);
            _clientIDs.Add(clientID);

            // Fire connection event
            serverOnClientConnect?.Invoke(clientID);
            
            // Fire on client connected for scene.
            NetworkSceneManager.OnClientConnected(clientID);
        }

        protected virtual void ConfigureServerFramerate() {
            // Unity server, unless stopped uses a stupidly high framerate
            #if UNITY_SERVER
            Application.targetFrameRate = networkConfig.serverTickRate;
            #endif
        }

        #endregion

        #region Client

        [HideInInspector] public UnityEvent clientOnConnect = new UnityEvent();

        [HideInInspector] public UnityEvent<string> clientOnDisconnect = new UnityEvent<string>();

        /// <summary>
        /// Start the manager in client mode.
        /// </summary>
        /// <param name="hostname">The hostname of the server to connect to.</param>
        /// <param name="connectionRequestData">Connection request data used for the approval stage.</param>
        /// <exception cref="NotSupportedException"></exception>
        public void StartClient(string hostname, byte[] connectionRequestData = null) {
            // Check that the transport is set.
            if (networkConfig.transport == null)
                throw new InvalidOperationException("The NetworkManager must be provided with a transport!");

            // Ensure manager isn't running.
            if (isSinglePlayer)
                throw new NotSupportedException("The network manager is already running in single player mode!");
            if (isHost)
                throw new NotSupportedException("The network manager is already running in host mode!");
            if (isServer)
                throw new NotSupportedException("The network manager is already running in server mode!");
            if (isClient)
                throw new NotSupportedException("A client is already running!");

            // Init
            Init();

            // Connect to the server
            networkConfig.transport.ClientConnect(hostname);
            // TODO: save the connection request data.

            // Start client
            isClient = true;
        }

        /// <summary>
        /// Stop the client
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if running in a different mode, or a client isn't running.</exception>
        public void StopClient() {
            if (isHost)
                throw new NotSupportedException("The network manager is running in host mode! Use StopHost().");
            if (isServer)
                throw new NotSupportedException("The network manager is running in server mode! Use StopServer().");
            if (!isClient)
                throw new NotSupportedException("A client is not running!");

            // Shutdown
            Shutdown();

            // Disconnect
            networkConfig.transport.ClientDisconnect();
            isClient = false;
        }

        /// <summary>
        /// Send data to the server as the client.
        /// </summary>
        /// <param name="packet">The packet to be sent.</param>
        /// <param name="channel">The channel to send with.</param>
        /// <typeparam name="TPacket">The packet type.</typeparam>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void ClientSend<TPacket>(TPacket packet, int channel = DefaultChannels.Reliable)
            where TPacket : ISerializable, new() {
            // Only allow sending of server packets.
            if (!PacketUtils.IsServerPacket<TPacket>())
                throw new InvalidOperationException("Attempted to send non-server packet to server!");

            // Host mode will not send data to the server
            if (isHost)
                return;

            // Write the data and send it with the transport
            using (var writer = NetworkWriterPool.GetWriter()) {
                // Write packet
                WritePacket(packet, writer);

                // Send with transport
                networkConfig.transport.ClientSend(writer.ToArraySegment(), channel);
            }
        }

        private void ClientOnConnected() {
            // Send connection request
            var request = new ConnectionRequestPacket
                          {connectionData = null, verificationHash = networkConfig.GetHash()};
            ClientSend(request);

            StartCoroutine(ClientApprovalTimeout()); // TODO: Maybe move this to the initial connect call?
        }

        private IEnumerator ClientApprovalTimeout() {
            var timeBegan = Time.unscaledTime;
            while (Time.unscaledTime - timeBegan < networkConfig.connectionRequestTimeout && connectedClients.Count < 1)
                yield return null;

            if (connectedClients.Count == 0) {
                Debug.Log("Server did not approve connection request in a timely fashion");
                Shutdown();
            }
        }

        private string _disconnectionReason;

        private void ClientOnDisconnected() {
            // Remove from connected clients list
            connectedClients.Remove(localClientID);

            // Fire disconnect event
            clientOnDisconnect?.Invoke(_disconnectionReason);
        }

        private void ClientOnDataReceived(ArraySegment<byte> data, int channel) {
            HandlePacket(0, data, channel);
        }

        private void ClientHandleApproval(ConnectionApprovedPacket packet) {
            // Save my client ID
            _localClientID = packet.localClientID;

            // Add to connections list
            connectedClients.Add(localClientID, new NetworkedClient {clientID = localClientID});

            // Fire connection event.
            clientOnConnect?.Invoke();
        }

        private void ClientHandleDisconnectRequest(ClientDisconnectPacket packet) {
            _disconnectionReason = packet.disconnectReason;
            networkConfig.transport.ClientDisconnect();
        }

        #endregion

        #region Host

        public void StartHost() {
            // Check that the transport is set.
            if (networkConfig.transport == null)
                throw new InvalidOperationException("The NetworkManager must be provided with a transport!");

            if (isSinglePlayer)
                throw new NotSupportedException("The network manager is already running in single player mode!");
            if (isClient && !isServer)
                throw new NotSupportedException("The network manager is already running in client mode!");
            if (isServer && !isClient)
                throw new NotSupportedException("The network manager is already running in server mode!");
            if (isHost)
                throw new NotSupportedException("Host mode is already running!");

            // Init
            Init();

            // Start server
            networkConfig.transport.ServerStart();
            isServer = true;
            isClient = true;

            connectedClients.Add(ServerLocalID, new NetworkedClient {
                                                                        clientID = ServerLocalID
                                                                    });
            connectedClientsList.Add(connectedClients[ServerLocalID]);

            // Set starting scene.
            SpawnManager.ServerSpawnSceneObjects(NetworkSceneManager.RegisterInitialScene());

            // Fire starting events.
            serverOnClientConnect?.Invoke(ServerLocalID);
            clientOnConnect?.Invoke();
            
            // Fire on client connected for scene.
            NetworkSceneManager.OnClientConnected(ServerLocalID);
        }

        public void StopHost() {
            if (isSinglePlayer)
                throw new
                    NotSupportedException("The network manager is running in single player mode! Use StopSinglePlayer().");
            if (isClient && !isServer)
                throw new NotSupportedException("The network manager is running in client mode! Use StopClient().");
            if (isServer && !isClient)
                throw new NotSupportedException("The network manager is running in server mode! Use StopServer().");
            if (!isHost)
                throw new NotSupportedException("A client is not running!");

            // Fire events
            serverOnClientDisconnect?.Invoke(ServerLocalID);
            clientOnDisconnect?.Invoke(null);

            // Shutdown
            Shutdown();

            // Stop server
            networkConfig.transport
                         .ServerShutdown(); // TODO: Thread safe way for transport to send events to the manager.
            isServer = false;
            isClient = false;
        }

        #endregion

        #region Single player

        /// <summary>
        /// Runs the game as if there was a server, without running one.
        /// </summary>
        public void StartSinglePlayer() {
            // Prevent incorrect use!
            if (isSinglePlayer)
                throw new NotSupportedException("The network manager is already running in single player mode!");
            if (isClient && !isServer)
                throw new NotSupportedException("The network manager is already running in client mode!");
            if (isServer && !isClient)
                throw new NotSupportedException("The network manager is already running in server mode!");
            if (isHost)
                throw new NotSupportedException("The network manager is already running in host mode!");

            // Init
            Init();

            // "Start" server
            isServer = true;
            isClient = true;
            isSinglePlayer = true;

            // Add local client
            connectedClients.Add(ServerLocalID, new NetworkedClient {
                                                                        clientID = ServerLocalID
                                                                    });
            connectedClientsList.Add(connectedClients[ServerLocalID]);

            // Set starting scene.
            SpawnManager.ServerSpawnSceneObjects(NetworkSceneManager.RegisterInitialScene());

            // Fire starting events.
            clientOnConnect?.Invoke();
            serverOnClientConnect?.Invoke(ServerLocalID);
            
            // Fire on client connected for scene.
            NetworkSceneManager.OnClientConnected(ServerLocalID);
        }

        /// <summary>
        /// Stops running the game in single player mode.
        /// </summary>
        public void StopSinglePlayer() {
            // Prevent incorrect use!
            if (isClient && !isServer)
                throw new NotSupportedException("The network manager is running in client mode! Use StopClient().");
            if (isServer && !isClient)
                throw new NotSupportedException("The network manager is running in server mode! Use StopServer().");
            if (isHost && !isSinglePlayer)
                throw new NotSupportedException("The network manager is running in host mode! Use StopHost().");
            if (!isSinglePlayer)
                throw new NotSupportedException("The network manager is not running in single player mode!");

            // Fire events
            serverOnClientDisconnect?.Invoke(ServerLocalID);
            clientOnDisconnect?.Invoke(null);

            // Shutdown
            Shutdown();

            // Stop server
            isServer = false;
            isClient = false;
            isSinglePlayer = false;
        }

        /// <summary>
        /// Converts single player game to host game.
        /// </summary>
        public void SinglePlayerStartHost() {
            // Check that the transport is set.
            if (networkConfig.transport == null)
                throw new InvalidOperationException("The NetworkManager must be provided with a transport!");

            // Start the server.
            networkConfig.transport.ServerStart();

            // Mark as host instead
            isSinglePlayer = false;
        }

        #endregion

        #region Common Initialization

        private void Init() {
            // TODO: Make this do more, such as look for prefab hash collisions etc.

            // Clear all lists and dictionaries
            connectedClients.Clear();
            connectedClientsList.Clear();
            serverHandlers.Clear();
            clientHandlers.Clear();

            // Clear disconnection reason because
            _disconnectionReason = null;

            // Save current state and set the desired state
            _wasRunningInBackground = Application.runInBackground;
            Application.runInBackground = runInBackground;

            // Hook transport events
            networkConfig.transport.onClientConnected.AddListener(ClientOnConnected);
            networkConfig.transport.onClientDisconnected.AddListener(ClientOnDisconnected);
            networkConfig.transport.onClientDataReceived.AddListener(ClientOnDataReceived);
            networkConfig.transport.onServerConnected.AddListener(ServerOnClientConnect);
            networkConfig.transport.onServerDataReceived.AddListener(ServerOnDataReceived);
            networkConfig.transport.onServerDisconnected.AddListener(ServerOnClientDisconnect);

            // Initial protocols
            RegisterBuiltinPackets();
        }

        private void Shutdown() {
            SpawnManager.DestroyNonSceneObjects();
            if (isServer) {
                SpawnManager.ServerUnspawnAllSceneObjects();
            }

            // Reset run in background state. So if player goes into main menu and minimizes the game stops using resources.
            Application.runInBackground = _wasRunningInBackground;

            // Unhook transport events
            networkConfig.transport.onClientConnected.RemoveListener(ClientOnConnected);
            networkConfig.transport.onClientDisconnected.RemoveListener(ClientOnDisconnected);
            networkConfig.transport.onClientDataReceived.RemoveListener(ClientOnDataReceived);
            networkConfig.transport.onServerConnected.RemoveListener(ServerOnClientConnect);
            networkConfig.transport.onServerDataReceived.RemoveListener(ServerOnDataReceived);
            networkConfig.transport.onServerDisconnected.RemoveListener(ServerOnClientDisconnect);
        }

        #endregion

        #region Packets

        internal readonly Dictionary<ulong, ClientPacketHandlers> clientHandlers =
            new Dictionary<ulong, ClientPacketHandlers>();

        internal readonly Dictionary<ulong, ServerPacketHandlers> serverHandlers =
            new Dictionary<ulong, ServerPacketHandlers>();

        /// <summary>
        /// Register a client packet's handler.
        /// </summary>
        /// <param name="handler">The handling action</param>
        /// <typeparam name="TPacket">The packet to be handled.</typeparam>
        public void RegisterClientPacketHandler<TPacket>(Action<TPacket> handler)
            where TPacket : ISerializable, new() {
            var packetID = GetPacketID<TPacket>();
            if (!clientHandlers.ContainsKey(packetID)) {
                clientHandlers.Add(packetID, PacketHandlers.GetClientHandlers(handler));
            } else Debug.LogWarning("Client packet handler was not registered as one already exists.");
        }

        /// <summary>
        /// Register a server packet's handler.
        /// </summary>
        /// <param name="handler">The handling action</param>
        /// <typeparam name="TPacket">The packet to be handled.</typeparam>
        public void RegisterServerPacketHandler<TPacket>(Action<ulong, TPacket> handler)
            where TPacket : ISerializable, new() {
            var packetID = GetPacketID<TPacket>();
            if (!serverHandlers.ContainsKey(packetID)) {
                serverHandlers.Add(packetID, PacketHandlers.GetServerHandlers(handler));
            } else Debug.LogWarning("Server packet handler was not registered as one already exists.");
        }

        /// <summary>
        /// Gets a packet ID.
        /// Uses the packet ID hash size from the config.
        /// </summary>
        /// <typeparam name="TPacket"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        internal ulong GetPacketID<TPacket>() where TPacket : ISerializable, new() {
            return GetPacketID(typeof(TPacket));
        }
        
        /// <summary>
        /// Gets a packet ID.
        /// Uses the packet ID hash size from the config.
        /// </summary>
        /// <typeparam name="TPacket"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        internal ulong GetPacketID(Type packetType) {
            switch (networkConfig.packetIDHashSize) {
                case HashSize.TwoBytes:
                    return PacketUtils.GetID16(packetType);
                case HashSize.FourBytes:
                    return PacketUtils.GetID32(packetType);
                case HashSize.EightBytes:
                    return PacketUtils.GetID64(packetType);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Registers all built in FNNLib packet handlers.
        /// </summary>
        private void RegisterBuiltinPackets() {
            // Protocol
            RegisterClientPacketHandler<ConnectionApprovedPacket>(ClientHandleApproval);
            RegisterClientPacketHandler<ClientDisconnectPacket>(ClientHandleDisconnectRequest);
            RegisterServerPacketHandler<ConnectionRequestPacket>(ServerHandleConnectionRequest);

            // Register scene management events.
            RegisterClientPacketHandler<SceneLoadPacket>(NetworkSceneManager.ClientHandleSceneLoadPacket);
            RegisterClientPacketHandler<SceneUnloadPacket>(NetworkSceneManager.ClientHandleSceneUnloadPacket);

            // Object spawning
            RegisterClientPacketHandler<SpawnObjectPacket>(SpawnManager.ClientHandleSpawnPacket);
            RegisterClientPacketHandler<DestroyObjectPacket>(SpawnManager.ClientHandleDestroy);

            // RPCs
            RegisterClientPacketHandler<RPCPacket>(NetworkBehaviour.ClientRPCCallHandler);
            RegisterServerPacketHandler<RPCPacket>(NetworkBehaviour.ServerRPCCallHandler);
        }

        /// <summary>
        /// Handles an incoming data stream by parsing as a packet.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        /// <param name="channel"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private void HandlePacket(ulong sender, ArraySegment<byte> data, int channel) {
            using (var reader = NetworkReaderPool.GetReader(data)) {
                ulong packetID;
                switch (networkConfig.packetIDHashSize) {
                    case HashSize.TwoBytes:
                        packetID = reader.ReadPackedUInt16();
                        break;
                    case HashSize.FourBytes:
                        packetID = reader.ReadPackedUInt32();
                        break;
                    case HashSize.EightBytes:
                        packetID = reader.ReadPackedUInt64();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // Fire the handler if present
                if (isServer) {
                    if (serverHandlers.TryGetValue(packetID, out var serverHandler)) {
                        serverHandler.packetDelegate(sender, reader);
                        return;
                    }
                } else {
                    if (clientHandlers.TryGetValue(packetID, out var clientHandler)) {
                        clientHandler.packetDelegate(reader);
                        return;
                    }
                }

                Debug.LogWarning("Ignoring unidentified packet.");
            }
        }

        /// <summary>
        /// Writes a packet into the writer stream.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="writer"></param>
        /// <typeparam name="TPacket"></typeparam>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WritePacket<TPacket>(TPacket packet, NetworkWriter writer) where TPacket : ISerializable, new() {
            // Write ID
            switch (networkConfig.packetIDHashSize) {
                case HashSize.TwoBytes:
                    writer.WritePackedUInt16(PacketUtils.GetID16<TPacket>());
                    break;
                case HashSize.FourBytes:
                    writer.WritePackedUInt32(PacketUtils.GetID32<TPacket>());
                    break;
                case HashSize.EightBytes:
                    writer.WritePackedUInt64(PacketUtils.GetID64<TPacket>());
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Write packet.
            writer.WritePackedObject(packet);
        }

        #endregion
    }
}