using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FNNLib.Messaging;
using FNNLib.Spawning;
using FNNLib.Transports;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace FNNLib.SceneManagement {
    [Serializable]
    public struct NetworkableScene {
        public string sceneName;
        public ScenePackingData scenePackingData;
    }

    /// <summary>
    /// This manages the scenes across the network.
    /// You can only change the scene using the server.
    /// On the client, it deals with the scene change packets.
    ///
    /// When a client joins the server, it will always join into the active scene.
    /// If building a party game, ensure the main scene is the lobby.
    /// </summary>
    public static class NetworkSceneManager {
        /// <summary>
        /// Other additive scenes that will be loaded when the player joins.
        /// </summary>
        private static List<NetworkScene> _mainScenes = new List<NetworkScene>();

        /// <summary>
        /// Dictionary of currently loaded scenes.
        /// </summary>
        internal static ConcurrentDictionary<uint, NetworkScene> loadedScenes =
            new ConcurrentDictionary<uint, NetworkScene>();
        
        internal static PacketBufferCollection<uint> bufferedScenePackets = new PacketBufferCollection<uint>();

        #region Scene Management

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// If a scene is loaded for everybody, new clients will also load this scene on connection.
        /// </remarks>
        /// <param name="sceneName"></param>
        /// <param name="serverMode"></param>
        /// <param name="clientMode"></param>
        /// <param name="clientsToLoadFor"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="NotImplementedException"></exception>
        public static NetworkScene LoadScene(string sceneName, LoadSceneMode serverMode, LoadSceneMode clientMode,
                                             List<ulong> clientsToLoadFor = null) {
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server may load scenes!");
            if (serverMode == LoadSceneMode.Single && clientMode == LoadSceneMode.Additive)
                throw new NotSupportedException();
            if (serverMode == LoadSceneMode.Single && clientsToLoadFor != null)
                throw new
                    NotSupportedException("If you load a scene in single mode on the server, all clients must be moved there. Pass null instead.");
            if (!CanSendClientTo(sceneName))
                throw new NotSupportedException();
            if (serverMode == LoadSceneMode.Additive && clientMode == LoadSceneMode.Single)
                throw new NotImplementedException();

            // Load on the server
            SceneManager.LoadScene(sceneName, serverMode);

            // Get a network ID
            var netID = GetSceneID();

            // Create NetworkScene
            var netScene = new NetworkScene {
                                                clientMode = clientMode,
                                                name = sceneName,
                                                netID = netID,
                                                scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1),
                                                serverMode = serverMode
                                            };
            loadedScenes.TryAdd(netID, netScene);

            // Spawn scene objects.
            SpawnManager.ServerSpawnSceneObjects(netID);

            // Register observers
            if (clientsToLoadFor == null) {
                // Add everyone to observers list.
                netScene.observers.AddRange(NetworkManager.instance.connectedClientsList.Select(item => item.clientID));

                // This is a main scene, add it
                if (serverMode == LoadSceneMode.Single)
                    _mainScenes.Clear();
                _mainScenes.Add(netScene);
            } else {
                // Add all clients to observers list
                netScene.observers.AddRange(clientsToLoadFor);
            }

            // Send load packet to all observers
            var loadPacket = new SceneLoadPacket {
                                                     mode = clientMode,
                                                     sceneIndex = GetSceneIndex(sceneName)
                                                 };
            NetworkManager.instance.ServerSend(netScene.observers, loadPacket, DefaultChannels.ReliableSequenced);
            foreach (var i in netScene.observers) {
                NetworkManager.instance.connectedClients[i].loadedScenes.Add(netID);
            }

            return netScene;
        }

        /// <summary>
        /// Unload a subscene, redirecting any present clients to the fallback scene.
        /// </summary>
        /// <param name="netID"></param>
        /// <param name="fallbackScene"></param>
        public static AsyncOperation UnloadSceneAsync(uint netID, uint fallbackScene) {
            if (NetworkManager.instance == null)
                return null;
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server may unload scenes!");
            if (!loadedScenes.ContainsKey(netID))
                throw new IndexOutOfRangeException();
            if (loadedScenes.Count <= 1)
                throw new NotSupportedException("Can only unload a scene if more than 1 scene is loaded!");
            if (!loadedScenes.ContainsKey(fallbackScene))
                throw new IndexOutOfRangeException("Fallback scene not loaded!");

            // Get the scene
            var netScene = loadedScenes[netID];

            // If this is a main scene, remove it from the list
            if (_mainScenes.Contains(netScene))
                _mainScenes.Remove(netScene);

            // Remove from dict and list
            loadedScenes.TryRemove(netScene.netID, out _);

            // Load the fallback scene on any observers that do not have it loaded
            foreach (var observer in netScene.observers) {
                if (!NetworkManager.instance.connectedClients[observer].loadedScenes.Contains(fallbackScene))
                    LoadSceneOnClient(observer, fallbackScene);
            }

            // Send unload to all observers
            var unloadPacket = new SceneUnloadPacket {sceneNetID = netID};
            NetworkManager.instance.ServerSend(netScene.observers, unloadPacket, DefaultChannels.ReliableSequenced);

            // Unload this scene.
            var op = SceneManager.UnloadSceneAsync(netScene.scene);
            Resources.UnloadUnusedAssets();
            return op;
        }

        internal static uint RegisterInitialScene() {
            // Change scene if the default scene is different
            var initialScene = NetworkManager.instance.networkConfig.initialScene;
            if (!string.IsNullOrEmpty(initialScene)) {
                if (!CanSendClientTo(initialScene))
                    throw new NotSupportedException("Initial scene is not on the networkable scenes list!");
                if (SceneManager.GetActiveScene().name != initialScene)
                    SceneManager.LoadScene(initialScene);
            }

            // Get active scene
            var scene = SceneManager.GetActiveScene();

            // Get existing ID if it is there.
            foreach (var subScene in loadedScenes) {
                if (subScene.Value.scene == scene)
                    return subScene.Key;
            }

            // Get ID
            var netID = GetSceneID();

            // Create net scene
            var netScene = new NetworkScene {scene = scene, netID = netID, name = scene.name};

            // Add to subscene list
            loadedScenes.TryAdd(netID, netScene);

            // Add to main scenes
            _mainScenes.Add(netScene);
            return netID;
        }

        /// <summary>
        /// Tell a client to load a scene that already exists on the server
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="networkID"></param>
        public static void LoadSceneOnClient(ulong clientID, uint networkID) {
            if (!loadedScenes.ContainsKey(networkID))
                throw new IndexOutOfRangeException();

            // Get the scene
            var scene = loadedScenes[networkID];

            // Add to observers list
            scene.observers.Add(clientID);

            // Send the load packet.
            var loadPacket = new SceneLoadPacket {
                                                     sceneNetworkID = networkID,
                                                     mode = scene.clientMode,
                                                     sceneIndex = GetSceneIndex(scene.name)
                                                 };
            NetworkManager.instance.ServerSend(clientID, loadPacket, DefaultChannels.ReliableSequenced);

            // Add to the loaded scenes list of the client
            NetworkManager.instance.connectedClients[clientID].loadedScenes.Add(networkID);

            // TODO: SpawnManager spawn all for client
        }

        /// <summary>
        /// Tells a list of clients to load a scene that already exists on the server
        /// </summary>
        /// <param name="clientIDs"></param>
        /// <param name="networkID"></param>
        public static void LoadSceneOnClients(List<ulong> clientIDs, uint networkID) {
            if (!loadedScenes.ContainsKey(networkID))
                throw new IndexOutOfRangeException();
            var scene = loadedScenes[networkID];
            var loadFallbackPacket = new SceneLoadPacket {
                                                             sceneNetworkID = networkID,
                                                             mode = scene.clientMode,
                                                             sceneIndex = GetSceneIndex(scene.name)
                                                         };
            NetworkManager.instance.ServerSend(clientIDs, loadFallbackPacket, DefaultChannels.ReliableSequenced);

            foreach (var clientID in clientIDs)
                // Add to the loaded scenes list of the client
                NetworkManager.instance.connectedClients[clientID].loadedScenes.Add(networkID);
        }

        #endregion

        #region Client and Object Management

        #endregion

        private static bool CanSendClientTo(string sceneName) {
            return NetworkManager.instance.networkConfig.networkableScenes.FindIndex((networkableScene) =>
                       networkableScene.sceneName == sceneName) != -1;
        }

        private static int GetSceneIndex(string sceneName) {
            return NetworkManager.instance.networkConfig.networkableScenes.FindIndex((networkableScene) =>
                networkableScene.sceneName == sceneName);
        }

        // private static ScenePackingData GetScenePackingData(string sceneName) {
        //     return NetworkManager.instance.networkConfig.networkableScenes.Find((networkableScene) =>
        //                                                                             networkableScene.sceneName == sceneName)
        //                          .scenePackingData;
        // }

        #region Client Handlers

        internal static void ClientHandleSceneLoadPacket(SceneLoadPacket packet) {
            // Get the scene to load
            var scene = NetworkManager.instance.networkConfig.networkableScenes[packet.sceneIndex];

            // Async load scene TODO: There are major problems around here. Need to buffer spawn packets and such
            var asyncLoad = SceneManager.LoadSceneAsync(scene.sceneName, packet.mode);
            var loadedScene = SceneManager.GetSceneAt(SceneManager
                                                   .sceneCount - 1);
            asyncLoad.completed += (AsyncOperation op) => {
                                       // Add to loaded scenes list
                                       loadedScenes.TryAdd(packet.sceneNetworkID, new NetworkScene {
                                                               scene = loadedScene,
                                                               clientMode = packet.mode,
                                                               name = scene.sceneName,
                                                               netID = packet.sceneNetworkID,
                                                           });

                                       // Prepare scene objects
                                       SpawnManager.ClientCollectSceneObjects(packet.sceneNetworkID,
                                                                              packet.mode == LoadSceneMode.Additive);

                                       // Add to the loaded scenes list of the client
                                       NetworkManager.instance.connectedClients[NetworkManager.instance.localClientID]
                                                     .loadedScenes.Add(packet.sceneNetworkID);
                                       
                                       // Run buffered actions (spawns)
                                       while (bufferedScenePackets.HasPending(packet.sceneNetworkID)) {
                                           bufferedScenePackets.ExecutePending(packet.sceneNetworkID);
                                       }

                                       bufferedScenePackets.DestroyQueue(packet.sceneNetworkID);
                                   };
        }

        internal static void ClientHandleSceneUnloadPacket(SceneUnloadPacket packet) {
            // Unload if its loaded
            if (loadedScenes.ContainsKey(packet.sceneNetID)) {
                SceneManager.UnloadSceneAsync(loadedScenes[packet.sceneNetID].scene);
                Resources.UnloadUnusedAssets();
                loadedScenes.TryRemove(packet.sceneNetID, out _);
            }
        }

        #endregion

        #region Server Handlers

        internal static void OnClientConnected(ulong clientID) {
            // Load all main scenes on the client.
            for (var i = 0; i < _mainScenes.Count; i++) {
                var netID = _mainScenes[i].netID;
                var scene = loadedScenes[netID];
                var loadFallbackPacket = new SceneLoadPacket {
                                                                 sceneNetworkID = netID,
                                                                 mode =
                                                                     i == 0 ? LoadSceneMode.Single : scene.clientMode,
                                                                 sceneIndex = GetSceneIndex(scene.name)
                                                             };
                NetworkManager.instance.ServerSend(clientID, loadFallbackPacket, DefaultChannels.ReliableSequenced);

                // Add to observers list
                loadedScenes[netID].observers.Add(clientID);

                // Add to the loaded scenes list of the client
                NetworkManager.instance.connectedClients[clientID].loadedScenes.Add(netID);

                // Spawn scene for player
                SpawnManager.OnClientJoinScene(clientID, netID);
            }
        }

        internal static void OnClientDisconnected(ulong clientID) { }

        #endregion

        #region Scene Fetching

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static uint GetSceneNetID(Scene scene) {
            foreach (var subScene in loadedScenes) {
                if (subScene.Value.scene == scene)
                    return subScene.Key;
            }

            // Not loaded.
            throw new Exception("Scene could not be found! Are you sure it is loaded?");
        }

        public static NetworkScene GetNetScene(Scene scene) {
            foreach (var subScene in loadedScenes) {
                if (subScene.Value.scene == scene)
                    return subScene.Value;
            }

            // Not loaded.
            throw new Exception("Scene could not be found! Are you sure it is loaded?");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="netID"></param>
        /// <returns></returns>
        public static NetworkScene GetNetScene(uint netID = 0) {
            return loadedScenes.ContainsKey(netID) ? loadedScenes[netID] : null;
        }

        #endregion

        #region Scene IDs

        // TODO: Recycle IDs

        private static uint _sceneIDCounter;

        private static uint GetSceneID() {
            _sceneIDCounter++;
            return _sceneIDCounter;
        }

        #endregion
    }
}