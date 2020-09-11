using System;
using System.Collections.Generic;
using FNNLib.Messaging;
using FNNLib.SceneManagement;
using FNNLib.Transports;
using UnityEngine;

namespace FNNLib.Core {
    /// <summary>
    /// The network manager drives the NetworkClient and NetworkServer systems.
    ///
    /// TODO: Make more of this virtual so that custom network managers could be made? Do we actually need this given its functionality/openness?
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
        public bool isClient { get; internal set; }
        
        /// <summary>
        /// Whether or not the game should run serverside code.
        /// </summary>
        public bool isServer { get; internal set; }
        
        /// <summary>
        /// Whether or not the client is a virtual client.
        /// </summary>
        public bool isHost => isClient && isServer;

        /// <summary>
        /// The local client ID.
        /// </summary>
        public int localClientID => isServer ? transport.serverClientID : _client.localClientID;
        
        /// <summary>
        /// The selected transport to be used by the client and server.
        /// </summary>
        public Transport transport;
        
        // TODO: Move a lot of the configuration for networking into its own serializable struct so it can be passed to transports. So these parameters arent on every transport.
        
        /// <summary>
        /// The protocol version of the client and server.
        /// Use this to stop old and new clients/servers interacting.
        /// </summary>
        public int protocolVersion;
        
        /// <summary>
        /// This enables the NetworkSceneManager and will register its packets in the relevant places.
        /// </summary>
        public bool useSceneManagement = true;
        
        /// <summary>
        /// The list of scenes that the server is permitted to send the client to.
        /// Used for security purposes if you opt to use the NetworkSceneManager (recommended).
        /// </summary>
        public List<NetworkableScene> permittedScenes = new List<NetworkableScene>();

        /// <summary>
        /// The server we are controlling.
        /// This can be accessed when the Server is running with NetworkServer.Instance or with the server property.
        /// </summary>
        private NetworkServer _server;

        /// <summary>
        /// The underlying server.
        /// </summary>
        public NetworkServer server => _server;
        
        /// <summary>
        /// The client we are controlling.
        /// This can be accessed when the Client is running with NetworkClient.Instance or with the client property.
        /// </summary>
        private NetworkClient _client;

        /// <summary>
        /// The underlying client.
        /// </summary>
        public NetworkClient client => _client;
        
        private void Awake() {
            // Instance manager
            if (instance != null && instance == this) {
                Debug.LogError("Only one NetworkManager may exist. Destroying.");
                Destroy(this);
            } else if (instance == null) instance = this;

            // Create client and server using protocol version
            _server = new NetworkServer(protocolVersion);
            _client = new NetworkClient(protocolVersion);
            
            // Add packets for features (if enabled)
            if (useSceneManagement) {
                _client.RegisterPacketHandler<SceneChangePacket>(NetworkSceneManager.ClientHandleSceneChangePacket);
            }
        }

        private void OnDestroy() {
            // Close host/server/client on destroy!
            if (isHost)
                StopHost();
            else if (isServer)
                StopServer();
            else if (isClient)
                StopClient();
        }
        
        #region Client

        /// <summary>
        /// Start the manager in client mode.
        /// </summary>
        /// <param name="hostname">The hostname of the server to connect to.</param>
        /// <param name="connectionRequestData">Connection request data used for the approval stage.</param>
        /// <exception cref="NotSupportedException"></exception>
        public void StartClient(string hostname, byte[] connectionRequestData = null) {
            // Ensure manager isn't running.
            if (isHost)
                throw new NotSupportedException("The network manager is already running in host mode!");
            if (isServer)
                throw new NotSupportedException("The network manager is already running in server mode!");
            if (isClient)
                throw new NotSupportedException("A client is already running!");
            
            // Activate transport
            transport.StartUsing();

            // Start client
            isClient = true;
            _client.Connect(hostname, connectionRequestData);
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
            
            // Deactivate transport
            transport.StopUsing();
        }
        
        #endregion
        
        #region Server

        /// <summary>
        /// Starts the manager in server mode.
        /// </summary>
        public void StartServer() {
            if (isHost)
                throw new NotSupportedException("The network manager is already running in host mode!");
            if (isClient)
                throw new NotSupportedException("The network manager is already running in client mode!");
            if (isServer)
                throw new NotSupportedException("A server is already running!");

            // Start transport
            transport.StartUsing();
            
            // Start server.
            isServer = true;
            _server.Start();
            
            // Hook stop event in case it closes.
            _server.onServerStopped.AddListener(OnServerStopped);
        }
        
        /// <summary>
        /// Stop a running server.
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        public void StopServer() {
            if (isHost)
                throw new NotSupportedException("The network manager is running in host mode! Use StopHost() instead.");
            if (isClient)
                throw new NotSupportedException("The network manager is running in client mode! Use StopClient() instead.");
            if (!isServer)
                throw new NotSupportedException("A server is not running!");
            
            // Stop server
            _server.Stop();
        }

        private void OnServerStopped(NetworkServer server) {
            // Remove hooks
            server.onServerStopped.RemoveListener(OnServerStopped);
            isServer = false;
            
            // Disable transport
            transport.StopUsing();
        }
        
        #endregion

        #region Host

        public void StartHost() {
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

        #region Registering Packets

        /// <summary>
        /// Registers a packet on the client and server, depending on it's target.
        /// </summary>
        /// <param name="handler"></param>
        /// <typeparam name="T"></typeparam>
        public void RegisterPacketHandler<T>(Action<int, T> handler) where T : IPacket, new() {
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