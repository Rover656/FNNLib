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
        private static ConcurrentDictionary<uint, NetworkScene> _loadedScenes =
            new ConcurrentDictionary<uint, NetworkScene>();
        
        #region Scene Management

        /// <summary>
        /// On single scene mode, this will load the scene and move all clients to it.
        /// On subscene mode, this will load the scene in and not move any clients there.
        /// To move clients there, use SendClientToScene. To make this the main/default scene, use SetActiveScene.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <returns>The scene's network ID.</returns>
        /// <exception cref="NotSupportedException"></exception>
        public static uint ServerLoadScene(string sceneName) {
            if (!NetworkManager.instance.networkConfig.useSceneManagement)
                throw
                    new NotSupportedException("The NetworkSceneManager is not enabled by the current NetworkManager!");
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server may load scenes!");
            if (!CanSendClientTo(sceneName))
                throw new
                    NotSupportedException("Cannot send client to this scene. It is not on the permitted scenes list.");

            // Load the scene
            if (NetworkManager.instance.networkConfig.enableSubScenes) {
                // Load the scene additively.
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
            }
            else {
                // Load the scene on the server then send everybody there.
                SceneManager.LoadScene(sceneName);
            }

            // Get the scene
            var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

            // Get a network ID
            var netID = GetSceneID();

            // Create net scene
            var netScene = new NetworkScene {scene = scene, sceneID = netID, sceneName = sceneName};

            // Add to subscene list
            _loadedScenes.TryAdd(netID, netScene);

            // Make main scene if no main scene exists or we're not in subscene mode
            if (!NetworkManager.instance.networkConfig.enableSubScenes || _activeScene == null) {
                _activeScene = netScene;
            }
            
            // TODO: If subscenes are enabled, we must move it based on packing data.

            // Move all clients if we're not in subscene mode.
            if (!NetworkManager.instance.networkConfig.enableSubScenes) {
                // Send the scene change packet
                var changePacket = new SceneChangePacket {sceneIndex = GetSceneIndex(sceneName), sceneNetID = netID};
                NetworkServer.instance.SendToAll(changePacket, DefaultChannels.ReliableSequenced);
            }

            return netID;
        }

        /// <summary>
        /// Sets the active/main scene with its index.
        /// This is the scene new clients will be sent to.
        /// </summary>
        /// <param name="netID"></param>
        public static void SetActiveScene(uint netID) {
            // Does nothing
            if (!NetworkManager.instance.networkConfig.enableSubScenes)
                return;
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server can set the active scene!");
            if (_loadedScenes.ContainsKey(netID))
                throw new IndexOutOfRangeException();

            // Make scene active.
            _activeScene = _loadedScenes[netID];
        }

        /// <summary>
        /// Unload a subscene, redirecting any clients to the fallback scene.
        /// TODO: Fallback scene packet so that player position can move.
        /// </summary>
        /// <param name="netID"></param>
        /// <param name="fallbackScene"></param>
        public static void ServerUnloadSubScene(uint netID, uint fallbackScene = 0) {
            if (NetworkManager.instance == null)
                return;
            if (!NetworkManager.instance.networkConfig.useSceneManagement)
                throw
                    new NotSupportedException("The NetworkSceneManager is not enabled by the current NetworkManager!");
            if (!NetworkManager.instance.networkConfig.enableSubScenes)
                throw new NotSupportedException("Subscenes are not enabled in the NetworkManager config!");
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server may unload scenes!");
            if (!_loadedScenes.ContainsKey(netID))
                throw new IndexOutOfRangeException();

            // Get the scene
            var netScene = _loadedScenes[netID];

            // Remove from dict and list
            _loadedScenes.TryRemove(netScene.sceneID, out _);

            // Send scene change packet to any observers of this scene.
            var changePacket = new SceneChangePacket {
                                                         sceneIndex =
                                                             GetSceneIndex(_loadedScenes[fallbackScene]
                                                                              .sceneName),
                                                         sceneNetID = _loadedScenes[fallbackScene].sceneID
                                                     };

            NetworkServer.instance.Send(netScene.observers, changePacket, DefaultChannels.ReliableSequenced);

            // Unload this scene.
            SceneManager.UnloadSceneAsync(netScene.scene);
        }
        
        #endregion
        
        #region Client and Object Management

        public static GameObject Instantiate(uint sceneID, GameObject go, Vector3 position, Quaternion rotation) {
            if (!_loadedScenes.ContainsKey(sceneID))
                throw new Exception("Scene is not loaded/does not exist!");
            var created = Object.Instantiate(go, position, rotation);
            SceneManager.MoveGameObjectToScene(created, _loadedScenes[sceneID].scene);
            return created;
        }
        
        // TODO: Moving networked objects...
        
        // TODO: FindObjectsOfType... Will be *very* slow but are kinda necessary. Need a performance warning for them.
        //       You should probably store lists of objects or use singletons to avoid these.
        //       Oh, and if you're not using subscenes they work the same as normal.

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="sceneID">The scene network ID.</param>
        /// <exception cref="NotSupportedException"></exception>
        public static void SendClientTo(ulong clientID, uint sceneID) {
            if (!NetworkManager.instance.networkConfig.useSceneManagement)
                throw
                    new NotSupportedException("The NetworkSceneManager is not enabled by the current NetworkManager!");
            if (!NetworkManager.instance.networkConfig.enableSubScenes)
                throw new NotSupportedException("SendClienTo can only be used if subscenes are enabled.");

            // Get client's current scene
            var client = NetworkManager.instance.connectedClients[clientID];

            // Remove from observers list
            _loadedScenes[client.sceneID].RemoveObserver(clientID);

            // Set the current scene
            NetworkManager.instance.connectedClients[clientID].sceneID = sceneID;

            // Send scene change packet.
            var changePacket = new SceneChangePacket {
                                                         sceneIndex =
                                                             GetSceneIndex(_loadedScenes[sceneID]
                                                                              .sceneName),
                                                         sceneNetID = _loadedScenes[sceneID].sceneID
                                                     };
            NetworkServer.instance.Send(clientID, changePacket, DefaultChannels.ReliableSequenced);
            NetworkManager.instance.connectedClients[clientID].sceneID = _loadedScenes[sceneID].sceneID;
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
            // Load the scene
            SceneManager.LoadScene(NetworkManager.instance.networkConfig.networkableScenes[packet.sceneIndex]
                                                 .sceneName);

            // Save my current scene ID
            NetworkManager.instance.connectedClients[NetworkManager.instance.localClientID].sceneID = packet.sceneNetID;

            // Confirm scene change.
            var confirmationPacket = new SceneChangeCompletedPacket {loadedSceneID = packet.sceneNetID};
            NetworkClient.instance.Send(confirmationPacket);
        }

        #endregion

        #region Server Handlers

        internal static void OnClientConnected(ulong clientID) {
            // If we don't have a main scene yet, add the current scene.
            if (_activeScene == null) {
                _activeScene = new NetworkScene {
                                                    scene = SceneManager.GetActiveScene(),
                                                    sceneName = SceneManager.GetActiveScene().name,
                                                    sceneID = GetSceneID()
                                                };
                _loadedScenes.TryAdd(_activeScene.sceneID, _activeScene);
            }

            // Send to the current scene.
            var changePacket = new SceneChangePacket
                               {sceneIndex = GetSceneIndex(_activeScene.sceneName), sceneNetID = _activeScene.sceneID};
            NetworkManager.instance.connectedClients[clientID].sceneID = _activeScene.sceneID;
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
            _loadedScenes[packet.loadedSceneID].AddObserver(clientID);

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
            foreach (var subScene in _loadedScenes) {
                if (subScene.Value.scene == scene)
                    return subScene.Key;
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
            return _loadedScenes.ContainsKey(netID) ? _loadedScenes[netID] : null;
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