using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FNNLib.Backend;
using FNNLib.Spawning;
using FNNLib.Transports;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

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
        /// The active/main scene.
        /// </summary>
        private static NetworkScene _activeScene;

        /// <summary>
        /// Dictionary of currently loaded scenes.
        /// </summary>
        internal static ConcurrentDictionary<uint, NetworkScene> loadedScenes =
            new ConcurrentDictionary<uint, NetworkScene>();

        #region Scene Management

        public static NetworkScene LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single) {
            return LoadScene(sceneName, mode, mode);
        }

        public static NetworkScene LoadScene(string sceneName, LoadSceneMode serverMode = LoadSceneMode.Single,
                                             LoadSceneMode clientMode = LoadSceneMode.Single) {
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server may load scenes!");
            if (!CanSendClientTo(sceneName))
                throw new
                    NotSupportedException("Cannot send client to this scene. It is not on the permitted scenes list.");
            if (serverMode == LoadSceneMode.Single && clientMode == LoadSceneMode.Additive)
                throw new
                    NotSupportedException("You cannot load a scene using single mode on server and additively on clients!");

            // We haven't implemented packing yet, prevent uses which need it
            if (serverMode == LoadSceneMode.Additive && clientMode == LoadSceneMode.Single)
                throw new NotImplementedException("Scene packing is not implemented");

            // Load the scene with the given server mode.
            SceneManager.LoadScene(sceneName, serverMode);

            // Get the scene
            var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

            // Get a network ID
            var netID = GetSceneID();

            // Create net scene
            var netScene = new NetworkScene {
                                                scene = scene, netID = netID, name = sceneName, serverMode = serverMode,
                                                clientMode = clientMode
                                            };

            // Add to subscene list
            loadedScenes.TryAdd(netID, netScene);

            // Make main scene if no main scene exists or we're not in subscene mode
            if (_activeScene == null) {
                _activeScene = netScene;
            }

            // TODO: If we are loading additively on the server, but not on the client we must move it based on packing data.

            // Move all clients if we're not in subscene mode.
            if (serverMode == LoadSceneMode.Single) {
                // Send the scene change packet
                var changePacket = new SceneChangePacket
                                   {sceneIndex = GetSceneIndex(sceneName), sceneNetID = netID, mode = clientMode};
                NetworkServer.instance.SendToAll(changePacket, DefaultChannels.ReliableSequenced);
            }

            // Spawn all of the scene objects
            SpawnManager.ServerSpawnSceneObjects(netID);

            return netScene;
        }

        /// <summary>
        /// Sets the active/main scene with its index.
        /// This is the scene new clients will be sent to.
        /// </summary>
        /// <param name="netID"></param>
        public static void SetActiveScene(uint netID) {
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server can set the active scene!");
            if (!loadedScenes.ContainsKey(netID))
                throw new IndexOutOfRangeException();

            // Make scene active.
            _activeScene = loadedScenes[netID];
        }

        public static void SetActiveScene(NetworkScene scene) {
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server can set the active scene!");
            if (scene == null)
                throw new ArgumentNullException();

            // Make scene active.
            _activeScene = scene;
        }

        public static NetworkScene GetActiveScene() {
            return _activeScene;
        }

        /// <summary>
        /// Unload a subscene, redirecting any clients to the fallback scene.
        /// TODO: Fallback scene packet so that player position can move.
        /// </summary>
        /// <param name="netID"></param>
        /// <param name="fallbackScene"></param>
        public static AsyncOperation UnloadSceneAsync(uint netID, uint fallbackScene = 0) {
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

            // If its the current scene, make the fallback scene main
            if (_activeScene.netID == netID)
                _activeScene = loadedScenes[fallbackScene];

            // Remove from dict and list
            loadedScenes.TryRemove(netScene.netID, out _);

            // Unspawn any scene objects
            SpawnManager.ServerUnspawnSceneObjects(netID);

            // Send scene change packet to any observers of this scene.
            var changePacket = new SceneChangePacket {
                                                         sceneIndex =
                                                             GetSceneIndex(loadedScenes[fallbackScene]
                                                                              .name),
                                                         sceneNetID = loadedScenes[fallbackScene].netID,
                                                         mode = loadedScenes[fallbackScene].clientMode
                                                     };
            NetworkServer.instance.Send(netScene.observers, changePacket, DefaultChannels.ReliableSequenced);

            // Unload this scene.
            var op = SceneManager.UnloadSceneAsync(netScene.scene);
            Resources.UnloadUnusedAssets();
            return op;
        }

        internal static uint RegisterActiveScene() {
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

            // Make main scene if no main scene exists or we're not in subscene mode
            if (_activeScene == null) {
                _activeScene = netScene;
            }

            return netID;
        }

        #endregion

        #region Client and Object Management

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="sceneID">The scene network ID.</param>
        /// <exception cref="NotSupportedException"></exception>
        public static void MoveClientToScene(ulong clientID, uint sceneID) {
            // Get client's current scene
            var client = NetworkManager.instance.connectedClients[clientID];

            // Remove from observers list
            loadedScenes[client.sceneID].RemoveObserver(clientID);

            // Set the current scene
            NetworkManager.instance.connectedClients[clientID].sceneID = sceneID;

            // Send scene change packet.
            var changePacket = new SceneChangePacket {
                                                         sceneIndex =
                                                             GetSceneIndex(loadedScenes[sceneID]
                                                                              .name),
                                                         sceneNetID = loadedScenes[sceneID].netID,
                                                         mode = loadedScenes[sceneID].clientMode
                                                     };
            NetworkServer.instance.Send(clientID, changePacket, DefaultChannels.ReliableSequenced);
            NetworkManager.instance.connectedClients[clientID].sceneID = loadedScenes[sceneID].netID;
            SpawnManager.OnClientChangeScene(clientID);
        }

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

        internal static void ClientHandleSceneChangePacket(ulong sender, SceneChangePacket packet) {
            // Get the target scene
            var targetScene = NetworkManager.instance.networkConfig.networkableScenes[packet.sceneIndex]
                                            .sceneName;
            var currentScene = SceneManager.GetActiveScene().name;

            // Reset the current scene if its the same
            if (targetScene == currentScene && packet.mode == LoadSceneMode.Single) {
                SpawnManager.DestroyNonSceneObjects();
                SpawnManager.ClientResetSceneObjects();
            }
            else {
                // Remove current loaded scene if it exists
                var curScene = NetworkManager.instance.connectedClients[NetworkManager.instance.localClientID].sceneID;
                if (loadedScenes.ContainsKey(curScene))
                    loadedScenes.TryRemove(curScene, out _);

                // Load the scene
                SceneManager.LoadScene(targetScene, packet.mode);
            }

            // Add to loaded scenes
            loadedScenes.TryAdd(packet.sceneNetID, new NetworkScene {
                                                                        scene = SceneManager.GetActiveScene(),
                                                                        netID = packet.sceneNetID,
                                                                        name = SceneManager.GetActiveScene().name
                                                                    });

            // Save my current scene ID
            NetworkManager.instance.connectedClients[NetworkManager.instance.localClientID].sceneID = packet.sceneNetID;

            // Setup scene spawns
            SpawnManager.ClientCollectSceneObjects(packet.sceneNetID);

            // Confirm scene change.
            var confirmationPacket = new SceneChangeCompletedPacket {loadedSceneID = packet.sceneNetID};
            NetworkClient.instance.Send(confirmationPacket);
        }

        #endregion

        #region Server Handlers

        internal static void OnClientConnected(ulong clientID) {
            // Send to the current scene.
            var changePacket = new SceneChangePacket {
                                                         sceneIndex = GetSceneIndex(_activeScene.name),
                                                         sceneNetID = _activeScene.netID,
                                                         mode = _activeScene.clientMode
                                                     };
            NetworkManager.instance.connectedClients[clientID].sceneID = _activeScene.netID;
            NetworkServer.instance.Send(clientID, changePacket, DefaultChannels.ReliableSequenced);
        }

        internal static void SceneChangeCompletedHandler(ulong clientID, SceneChangeCompletedPacket packet) {
            // Verify scene ID
            if (packet.loadedSceneID != NetworkManager.instance.connectedClients[clientID].sceneID) {
                // Ignore, they should set themselves right.
                // TODO: Some kind of timer that checks that they have swapped scene within 30 secs or so.
                return;
            }

            // Add to scene observer list
            loadedScenes[packet.loadedSceneID].AddObserver(clientID);

            // Spawn network objects
            SpawnManager.OnClientJoinScene(clientID, packet.loadedSceneID);
        }

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