using System;
using System.Collections;
using System.Collections.Generic;
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
    public delegate bool ApproveConnectionDelegate(ulong clientID, byte[] connectionData);
    
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

        /// <summary>
        /// Whether or not the game is running in single player mode.
        /// </summary>
        public bool isSinglePlayer { get; private set; }

        /// <summary>
        /// Whether or not the client is a virtual client.
        /// </summary>
        public bool isHost => isClient && isServer;

        /// <summary>
        /// The local client ID.
        /// </summary>
        public ulong localClientID => isServer ? ServerLocalID : _localClientID;

        /// <summary>
        /// The underlying client ID.
        /// </summary>
        private ulong _localClientID;

        /// <summary>
        /// Dictionary containing all connected clients.
        /// Key is client ID.
        /// </summary>
        public readonly Dictionary<ulong, NetworkedClient> connectedClients = new Dictionary<ulong, NetworkedClient>();

        /// <summary>
        /// List of all connected clients.
        /// </summary>
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

        /// <summary>
        /// Previous value of runInBackground so it can be reset once the manager is finished.
        /// </summary>
        private bool _wasRunningInBackground;

        /// <summary>
        /// The network config.
        /// </summary>
        [HideInInspector] public NetworkConfig networkConfig;

        /// <summary>
        /// Connection approval callback.
        /// Used for adding extra logic to connection acceptance.
        /// </summary>
        public ApproveConnectionDelegate connectionApprovalCallback = null;

        #region Editor
        
        /// <summary>
        /// Ensure the default channels are maintained
        /// </summary>
        private void OnValidate() {
            networkConfig.EnsureDefaultChannels();
        }
        
        #endregion
        
        #region Engine Management

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
        
        #endregion

        #region Server

        /// <summary>
        /// On client connect to server.
        /// </summary>
        [HideInInspector] public UnityEvent<ulong> serverOnClientConnect = new UnityEvent<ulong>();

        /// <summary>
        /// On client disconnect from server.
        /// </summary>
        [HideInInspector] public UnityEvent<ulong> serverOnClientDisconnect = new UnityEvent<ulong>();

        /// <summary>
        /// List of all clients pending connection approvals.
        /// </summary>
        private List<ulong> _pendingClients = new List<ulong>();
        
        /// <summary>
        /// List of all client IDs
        /// </summary>
        [HideInInspector]
        public List<ulong> allClientIDs = new List<ulong>();

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
            networkConfig.transport.ServerShutdown();
            isServer = false;
        }

        /// <summary>
        /// Disconnect a client with a reason
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="disconnectReason"></param>
        public void ServerDisconnect(ulong clientID, string disconnectReason) {
            // Send disconnect packet
            NetworkChannel.Reliable.ServerSend(clientID,
                                               new ClientDisconnectPacket {disconnectReason = disconnectReason});

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
        /// Handles a client connection.
        /// </summary>
        /// <param name="clientID">The client that connected</param>
        private void ServerOnClientConnect(ulong clientID) {
            // Add to the pending clients and begin connection request timeout
            _pendingClients.Add(clientID);

            // Start disconnect coroutine
            StartCoroutine(ClientConnectionTimeout(clientID));
        }

        /// <summary>
        /// Handles a client disconnection.
        /// </summary>
        /// <param name="clientID"></param>
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

            if (allClientIDs.Contains(clientID))
                allClientIDs.Remove(clientID);

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

        /// <summary>
        /// Handle a connection request packet.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="packet"></param>
        /// <param name="sender"></param>
        private void ServerHandleConnectionRequest(NetworkChannel channel, ConnectionRequestPacket packet, ulong sender) {
            // Ignore extra requests.
            if (connectedClients.ContainsKey(sender))
                return;

            // Remove from pending clients list
            if (_pendingClients.Contains(sender))
                _pendingClients.Remove(sender);

            // Check hashes
            if (packet.verificationHash != networkConfig.GetHash()) {
                ServerDisconnect(sender, "Client version does not match server!");
                return;
            }
            
            // Check custom callback
            if (connectionApprovalCallback != null) {
                if (!connectionApprovalCallback(sender, packet.connectionData)) {
                    ServerDisconnect(sender, "Connection approval callback rejected client.");
                    return;
                }
            }

            // Send approval
            channel.ServerSend(sender, new ConnectionApprovedPacket {localClientID = sender});

            // Add client to connected clients
            connectedClients.Add(sender, new NetworkedClient {
                                                                 clientID = sender
                                                             });
            connectedClientsList.Add(connectedClients[sender]);
            allClientIDs.Add(sender);

            // Fire connection event
            serverOnClientConnect?.Invoke(sender);

            // Fire on client connected for scene.
            NetworkSceneManager.OnClientConnected(sender);
        }

        /// <summary>
        /// Configure the server's framerate to prevent high CPU usage.
        /// </summary>
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

        private byte[] _connectionRequestData;

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
            _connectionRequestData = connectionRequestData;
            networkConfig.transport.ClientConnect(hostname);

            // Start client
            isClient = true;

            // Add connection timeout
            StartCoroutine(ClientApprovalTimeout());
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
        /// Handle client connection.
        /// </summary>
        private void ClientOnConnected() {
            // Send connection request
            var request = new ConnectionRequestPacket
                          {connectionData = _connectionRequestData, verificationHash = networkConfig.GetHash()};
            NetworkChannel.Reliable.ClientSend(request);
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

        /// <summary>
        /// The reason we have been provided for our disconnection.
        /// </summary>
        private string _disconnectionReason;

        /// <summary>
        /// Handle client disconnection
        /// </summary>
        private void ClientOnDisconnected() {
            // Remove from connected clients list
            connectedClients.Remove(localClientID);

            // Fire disconnect event
            clientOnDisconnect?.Invoke(_disconnectionReason);
        }

        private void ClientHandleApproval(NetworkChannel channel, ConnectionApprovedPacket packet) {
            // Save my client ID
            _localClientID = packet.localClientID;

            // Add to connections list
            connectedClients.Add(localClientID, new NetworkedClient {clientID = localClientID});

            // Fire connection event.
            clientOnConnect?.Invoke();
        }

        private void ClientHandleDisconnectRequest(NetworkChannel channel, ClientDisconnectPacket packet) {
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
            networkConfig.transport.ServerShutdown();
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

        /// <summary>
        /// Initialize the network manager.
        /// </summary>
        private void Init() {
            // TODO: Make this do more, such as look for prefab hash collisions etc.

            // Clear all lists and dictionaries
            connectedClients.Clear();
            connectedClientsList.Clear();

            // Reset networking channels
            networkConfig.EnsureDefaultChannels();
            NetworkChannel.Reliable.ResetChannel();
            NetworkChannel.ReliableSequenced.ResetChannel();
            NetworkChannel.Unreliable.ResetChannel();

            // Clear disconnection reason because
            _disconnectionReason = null;

            // Save current state and set the desired state
            _wasRunningInBackground = Application.runInBackground;
            Application.runInBackground = runInBackground;

            // Initial protocols
            InternalRegisterMessages();
        }

        /// <summary>
        /// Shutdown all of the manager.
        /// </summary>
        private void Shutdown() {
            // Shut down spawn manager.
            SpawnManager.DestroyNonSceneObjects();
            if (isServer) {
                SpawnManager.ServerUnspawnAllSceneObjects();
            }

            // Reset run in background state. So if player goes into main menu and minimizes the game stops using resources.
            Application.runInBackground = _wasRunningInBackground;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// The last time we purged all buffers.
        /// </summary>
        private float _lastBufferPurge;

        /// <summary>
        /// Run network events.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private void LateUpdate() {
            if (!enabled) return;

            // Prioritise server. Host mode will take precedence (obviously).
            if (isServer) {
                // Process server events
                for (var i = 0; i < networkConfig.serverMaxReceivesPerUpdate; i++) {
                    var eventType = networkConfig.transport.GetMessage(out var clientID, out var data, out var channel);

                    switch (eventType) {
                        case NetworkEventType.None:
                            goto exit;
                        case NetworkEventType.Connected:
                            ServerOnClientConnect(clientID);
                            break;
                        case NetworkEventType.Data:
                            if (channel < networkConfig.channels.Count) {
                                networkConfig.channels[channel]
                                             .HandleIncoming(clientID, NetworkReaderPool.GetReader(data), true);
                            } else {
                                Debug.LogWarning("Channel not registered!");
                            }

                            break;
                        case NetworkEventType.Disconnected:
                            ServerOnClientDisconnect(clientID);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            } else if (isClient) {
                for (var i = 0; i < networkConfig.clientMaxReceivesPerUpdate; i++) {
                    var eventType = networkConfig.transport.GetMessage(out _, out var data, out var channel);

                    switch (eventType) {
                        case NetworkEventType.None:
                            goto exit;
                        case NetworkEventType.Connected:
                            ClientOnConnected();
                            break;
                        case NetworkEventType.Data:
                            if (channel < networkConfig.channels.Count) {
                                networkConfig.channels[channel].HandleIncoming(ServerLocalID,
                                                                               NetworkReaderPool.GetReader(data), 
                                                                               false);
                            } else {
                                Debug.LogWarning("Channel not registered!");
                            }

                            break;
                        case NetworkEventType.Disconnected:
                            ClientOnDisconnected();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            exit:

            // Purge all expired but still buffered packets
            if (Time.unscaledTime - _lastBufferPurge > (1f / networkConfig.packetBufferPurgesPerSecond)) {
                _lastBufferPurge = Time.unscaledTime;
                BasePacketBufferCollection.PurgeAllOldPackets();
            }

            // TODO: Move other timeout checks here.
        }

        #endregion

        #region Messages

        /// <summary>
        /// Registers all built in FNNLib packet handlers.
        /// </summary>
        private void InternalRegisterMessages() {
            // Protocol
            NetworkChannel.Reliable.GetFactory()
                          .ClientConsumer<ConnectionApprovedPacket>(ClientHandleApproval).Register();
            NetworkChannel.Reliable.GetFactory()
                          .ClientConsumer<ClientDisconnectPacket>(ClientHandleDisconnectRequest).Register();
            NetworkChannel.Reliable.GetFactory()
                          .ServerConsumer<ConnectionRequestPacket>(ServerHandleConnectionRequest).Register();

            // Register scene management events.
            NetworkChannel.ReliableSequenced.GetFactory()
                          .ClientConsumer<SceneLoadPacket>(NetworkSceneManager
                                                              .ClientHandleSceneLoadPacket).Register();
            NetworkChannel.ReliableSequenced.GetFactory()
                          .ClientConsumer<SceneUnloadPacket>(NetworkSceneManager.ClientHandleSceneUnloadPacket)
                          .Register();
            NetworkChannel.ReliableSequenced.GetFactory()
                          .ClientConsumer<MoveObjectToScenePacket>(NetworkSceneManager.ClientHandleMoveObjectPacket)
                          .Buffered().Register();

            // Object spawning
            NetworkChannel.ReliableSequenced.GetFactory()
                          .ClientConsumer<SpawnObjectPacket>(SpawnManager.ClientHandleSpawnPacket).Buffered()
                          .Register();
            NetworkChannel.ReliableSequenced.GetFactory()
                          .ClientConsumer<DestroyObjectPacket>(SpawnManager.ClientHandleDestroy).Buffered()
                          .Register();
            NetworkChannel.ReliableSequenced.GetFactory()
                          .ClientConsumer<OwnerChangedPacket>(NetworkIdentity.OnOwnershipChanged).Buffered();

            // RPCs
            NetworkChannel.ReliableSequenced.GetFactory()
                          .Consumer<RPCPacket>(NetworkBehaviour.RPCCallHandler).Buffered().Register();
            NetworkChannel.ReliableSequenced.GetFactory()
                          .Consumer<RPCResponsePacket>(RPCResponseManager.HandleRPCResponse).Register();

            // Replicated Vars
            NetworkChannel.Reliable.GetFactory(NetworkBehaviour.VAR_DELTA_ID).Consumer(NetworkBehaviour.HandleVarDelta)
                          .Register();
        }

        #endregion
    }
}