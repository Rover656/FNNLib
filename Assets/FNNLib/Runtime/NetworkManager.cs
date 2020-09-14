using System;
using System.Collections.Generic;
using FNNLib.Backend;
using FNNLib.Config;
using FNNLib.Messaging;
using FNNLib.SceneManagement;
using FNNLib.Spawning;
using FNNLib.Transports;
using UnityEngine;

namespace FNNLib {
    /// <summary>
    /// The network manager drives the NetworkClient and NetworkServer systems.
    ///
    /// TODO: Make more of this virtual so that custom network managers could be made? Do we actually need this given its functionality/openness?
    ///
    /// TODO: Allow changing hash strength.
    /// </summary>
    [AddComponentMenu("Networking/Network Manager")]
    public class NetworkManager : MonoBehaviour {
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
        /// Whether or not the client is a virtual client.
        /// </summary>
        public bool isHost => isClient && isServer;

        /// <summary>
        /// The local client ID.
        /// </summary>
        public ulong localClientID => isServer ? 0 : _client.localClientID;

        public readonly Dictionary<ulong, ConnectedClient> connectedClients = new Dictionary<ulong, ConnectedClient>();

        public readonly List<ConnectedClient> connectedClientsList = new List<ConnectedClient>();

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

        /// <summary>
        /// The server we are controlling.
        /// This can be accessed when the Server is running with NetworkServer.Instance.
        /// </summary>
        private NetworkServer _server;

        /// <summary>
        /// The client we are controlling.
        /// This can be accessed when the Client is running with NetworkClient.Instance.
        /// </summary>
        private NetworkClient _client;

        private void OnEnable() {
            // Instance manager
            if (instance != null && instance == this) {
                Debug.LogError("Only one NetworkManager may exist. Destroying.");
                Destroy(this);
            } else {
                instance = this;
                if (dontDestroyOnLoad)
                    DontDestroyOnLoad(this);
            }

            // Create client and server using config hash
            _server = new NetworkServer(networkConfig.GetHash());
            _client = new NetworkClient(networkConfig.GetHash());

            // Register scene management events.
            // TODO: Should this be put elsewhere?
            if (networkConfig.useSceneManagement) {
                _client.RegisterPacketHandler<SceneChangePacket>(NetworkSceneManager.ClientHandleSceneChangePacket);
                _server.onClientConnected.AddListener(NetworkSceneManager.OnClientConnected);
            }
            
            // Object spawning
            _client.RegisterPacketHandler<SpawnObjectPacket>(SpawnManager.ClientHandleSpawnPacket);
            _client.RegisterPacketHandler<DestroyObjectPacket>(SpawnManager.ClientHandleDestroy);
        }

        #region Server

        /// <summary>
        /// Starts the manager in server mode.
        /// </summary>
        public void StartServer() {
            // Check that the transport is set.
            if (networkConfig.transport == null)
                throw new InvalidOperationException("The NetworkManager must be provided with a transport!");

            if (isHost)
                throw new NotSupportedException("The network manager is already running in host mode!");
            if (isClient)
                throw new NotSupportedException("The network manager is already running in client mode!");
            if (isServer)
                throw new NotSupportedException("A server is already running!");

            // Init
            Init();

            // Start server.
            isServer = true;
            _server.Start(networkConfig.transport);

            // Hook stop event in case it closes.
            _server.onServerStopped.AddListener(OnServerStopped);
            _server.onClientConnected.AddListener(ServerOnClientConnected);
            _server.onClientDisconnected.AddListener(ServerOnClientDisconnected);

            // Server fps fix
            ConfigureServerFramerate();
        }

        /// <summary>
        /// Stop a running server.
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        public void StopServer() {
            if (isHost)
                throw new NotSupportedException("The network manager is running in host mode! Use StopHost() instead.");
            if (isClient)
                throw new
                    NotSupportedException("The network manager is running in client mode! Use StopClient() instead.");
            if (!isServer)
                throw new NotSupportedException("A server is not running!");

            // Stop server
            _server.Stop();
        }

        private void ServerOnClientConnected(ulong clientID) {
            if (!connectedClients.ContainsKey(clientID)) {
                connectedClients.Add(clientID, new ConnectedClient {
                                                                       clientID = clientID
                                                                   });
                connectedClientsList.Add(connectedClients[clientID]);
            }
        }

        private void ServerOnClientDisconnected(ulong clientID) {
            if (connectedClients.ContainsKey(clientID)) {
                // Destroy owned objects
                for (var i = connectedClients[clientID].ownedObjects.Count - 1; i > -1; i--) {
                    SpawnManager.OnDestroy(connectedClients[clientID].ownedObjects[i].networkID, true);
                }
                
                // Destroy player object
                if (connectedClients[clientID].playerObject != null)
                    SpawnManager.OnDestroy(connectedClients[clientID].playerObject.networkID, true);
                
                connectedClientsList.Remove(connectedClients[clientID]);
                connectedClients.Remove(clientID);
            }
        }

        private void OnServerStopped(NetworkServer server) {
            // Remove hooks
            server.onServerStopped.RemoveListener(OnServerStopped);
            _server.onClientConnected.RemoveListener(ServerOnClientConnected);
            _server.onClientDisconnected.RemoveListener(ServerOnClientDisconnected);
            isServer = false;
        }

        protected virtual void ConfigureServerFramerate() {
            // Unity server, unless stopped uses a stupidly high framerate
            #if UNITY_SERVER
            Application.targetFrameRate = networkConfig.serverTickRate;
            #endif
        }

        #endregion

        #region Client

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
            if (isHost)
                throw new NotSupportedException("The network manager is already running in host mode!");
            if (isServer)
                throw new NotSupportedException("The network manager is already running in server mode!");
            if (isClient)
                throw new NotSupportedException("A client is already running!");

            // Init
            Init();

            // Start client
            isClient = true;
            _client.Connect(networkConfig.transport, hostname, connectionRequestData);
            _client.onConnected.AddListener(ClientOnConnected);
            _client.onDisconnected.AddListener(ClientOnDisconnected);
        }

        /// <summary>
        /// Stop client mode
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if running in a different mode, or a client isn't running.</exception>
        public void StopClient() {
            if (isHost)
                throw new NotSupportedException("The network manager is running in host mode! Use StopHost().");
            if (isServer)
                throw new NotSupportedException("The network manager is running in server mode! Use StopServer().");
            if (!isClient)
                throw new NotSupportedException("A client is not running!");

            // Disconnect
            _client.Disconnect();
            isClient = false;
        }

        private void ClientOnConnected() {
            connectedClients.Add(localClientID, new ConnectedClient {
                                                                        clientID = localClientID
                                                                    });
        }

        private void ClientOnDisconnected(string reason) {
            _client.onConnected.RemoveListener(ClientOnConnected);
            _client.onDisconnected.RemoveListener(ClientOnDisconnected);
            connectedClients.Remove(localClientID);
        }

        #endregion

        #region Host

        public void StartHost() {
            // Check that the transport is set.
            if (networkConfig.transport == null)
                throw new InvalidOperationException("The NetworkManager must be provided with a transport!");

            if (isClient && !isServer)
                throw new NotSupportedException("The network manager is already running in client mode!");
            if (isServer && !isClient)
                throw new NotSupportedException("The network manager is already running in server mode!");
            if (isHost)
                throw new NotSupportedException("Host mode is already running!");

            // TODO: Host mode implementation
            // TODO: Host will be able to skip the whole connection approval process... we need to implement this.
        }

        public void StopHost() {
            if (isClient && !isServer)
                throw new NotSupportedException("The network manager is running in client mode! Use StopHost().");
            if (isServer && !isClient)
                throw new NotSupportedException("The network manager is running in server mode! Use StopServer().");
            if (!isHost)
                throw new NotSupportedException("A client is not running!");

            // TODO: Host mode implementation
        }

        #endregion

        #region Single player

        // TODO: In the future, I could add a single player, which will simply do the same as host, without running a networked server at all.
        //        It wouldn't take much because host already deals with the propogation of events around the virtual client, we just need to turn off the networked side completely. 

        #endregion

        #region Common Initialization

        private void Init() {
            // TODO: Make this do more, such as look for prefab hash collisions etc.
            connectedClients.Clear();
            connectedClientsList.Clear();

            // Save current state and set the desired state
            _wasRunningInBackground = Application.runInBackground;
            Application.runInBackground = runInBackground;
        }

        private void Cleanup() {
            // Destroy all spawned objects
            for (var i = SpawnManager.spawnedObjectsList.Count - 1; i > -1; i--) {
                SpawnManager.OnDestroy(SpawnManager.spawnedObjectsList[i].networkID, true);
            }
            
            // Reset
            Application.runInBackground = _wasRunningInBackground;
        }

        #endregion

        #region Registering Packets

        /// <summary>
        /// Registers a packet on the client and server, depending on it's target.
        /// </summary>
        /// <param name="handler"></param>
        /// <typeparam name="T"></typeparam>
        public void RegisterPacketHandler<T>(Action<ulong, T> handler) where T : IPacket, new() {
            if (PacketUtils.IsClientPacket<T>()) {
                _client.RegisterPacketHandler(handler);
            }

            if (PacketUtils.IsServerPacket<T>()) {
                _server.RegisterPacketHandler(handler);
            }
        }

        #endregion
    }
}