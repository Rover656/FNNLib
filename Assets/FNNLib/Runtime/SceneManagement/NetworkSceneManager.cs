using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FNNLib.Backend;
using FNNLib.Transports;
using UnityEngine.SceneManagement;

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
        /// Dictionary of currently active subscenes.
        /// </summary>
        private static ConcurrentDictionary<uint, NetworkScene> _subScenes;

        private static List<NetworkScene> _subScenesList;

        /// <summary>
        /// On single scene mode, this will load the scene and move all clients to it.
        /// On subscene mode, this will load the scene in and not move any clients there.
        /// To move clients there, use SendClientToScene. To make this the main/default scene, use SetActiveScene.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <exception cref="NotSupportedException"></exception>
        public static void ServerLoadScene(string sceneName) {
            if (!NetworkManager.instance.networkConfig.useSceneManagement)
                throw new NotSupportedException("The NetworkSceneManager is not enabled by the current NetworkManager!");
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server may load scenes!");
            if (!CanSendClientTo(sceneName))
                throw new
                    NotSupportedException("Cannot send client to this scene. It is not on the permitted scenes list.");

            if (NetworkManager.instance.networkConfig.enableSubScenes) {
                // Load the scene additively.
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
                var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                
                // Get a network ID
                var netID = GetSceneID();
                
                // Create net scene
                var netScene = new NetworkScene {scene = scene, sceneID = netID, sceneName = sceneName};
                
                // Add to subscene list
                _subScenes.TryAdd(netID, netScene);
                _subScenesList.Add(netScene);
            } else {
                // Load the scene on the server then send everybody there.
                SceneManager.LoadScene(sceneName);
                var scene = SceneManager.GetActiveScene();

                // Get network ID
                var netID = GetSceneID();

                // Set the current scene
                _activeScene = new NetworkScene {scene = scene, sceneID = netID, sceneName = sceneName};

                // Send the scene change packet
                var changePacket = new SceneChangePacket {sceneIndex = GetSceneIndex(sceneName), sceneNetID = netID};
                NetworkServer.instance.SendToAll(changePacket, DefaultChannels.ReliableSequenced);
            }
        }

        /// <summary>
        /// Sets the active/main scene with its index.
        /// </summary>
        /// <param name="index"></param>
        public static void SetActiveScene(int index) {
            // Does nothing
            if (!NetworkManager.instance.networkConfig.enableSubScenes)
                return;
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server can set the active scene!");
            if (index >= _subScenesList.Count)
                throw new IndexOutOfRangeException();

            // Grab each scene.
            var newActiveScene = _subScenesList[index];
            var curActiveScene = _activeScene;
            
            // Remove new scene from list and dict and make it active.
            _subScenes.TryRemove(newActiveScene.sceneID, out _);
            _subScenesList.Remove(newActiveScene);
            _activeScene = newActiveScene;
            
            // Move the last scene into subscene list.
            _subScenes.TryAdd(curActiveScene.sceneID, curActiveScene);
            _subScenesList.Add(curActiveScene);
        }

        /// <summary>
        /// Load a sub scene on the server side ready for accepting clients.
        /// This will use the packing data in the networked scene to spawn it in a free location.
        /// TODO: Have a root scene that the server will live in.
        /// </summary>
        /// <param name="sceneName">The scene to be loaded.</param>
        public static void ServerLoadSubScene(string sceneName) {
            if (NetworkManager.instance == null)
                return;
            if (!NetworkManager.instance.networkConfig.useSceneManagement)
                throw
                    new NotSupportedException("The NetworkSceneManager is not enabled by the current NetworkManager!");
            if (!NetworkManager.instance.networkConfig.enableSubScenes)
                throw new NotSupportedException("Subscenes are not enabled in the NetworkManager config!");
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server may load scenes!");
            if (!CanSendClientTo(sceneName))
                throw new
                    NotSupportedException("Cannot send client to this scene. It is not on the permitted scenes list.");

            throw new
                NotImplementedException("Subscenes will be revisited once the regular scene system works. This is so I can ensure the system works before adding more advanced features...");

            // Load the scene in the game
            SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

            // TODO: Offset all of these objects to the room position
            var rootObjects = scene.GetRootGameObjects();
        }

        /// <summary>
        /// Unload a subscene, redirecting any clients to the fallback scene.
        /// TODO: Fallback scene packet so that player position can move.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="fallbackScene"></param>
        public static void ServerUnloadSubScene(string sceneName, int fallbackScene = 0) {
            if (NetworkManager.instance == null)
                return;
            if (!NetworkManager.instance.networkConfig.useSceneManagement)
                throw
                    new NotSupportedException("The NetworkSceneManager is not enabled by the current NetworkManager!");
            if (!NetworkManager.instance.networkConfig.enableSubScenes)
                throw new NotSupportedException("Subscenes are not enabled in the NetworkManager config!");
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server may unload scenes!");
            // TODO
        }

        public static void SendClientTo(ulong clientID, string sceneName) {
            if (!NetworkManager.instance.networkConfig.useSceneManagement)
                throw
                    new NotSupportedException("The NetworkSceneManager is not enabled by the current NetworkManager!");
            // TODO: Will this only be used for subscenes (i.e. games with more than 1 scene at a time). Probably.
            // TODO: Move the client to the scene.
        }

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
            /*
             * The process:
             * - Start a transition period (some form of waiting state where the game doesn't freeze, but nothing is happening)
             * - Change scene first (we don't have to save networked objects, because they will be spawned by the server once we confirm the scene change was successful)
             * - Once the scene has changed, tell the server that the scene change was successful, then the server will begin sending us networked objects.
             */

            // Load the scene
            SceneManager.LoadScene(NetworkManager.instance.networkConfig.networkableScenes[packet.sceneIndex]
                                                 .sceneName);

            // TODO: Send scene join confirmation packet so that we may be added to the observers list for any applicable network objects.
        }

        #endregion

        #region Server Handlers

        internal static void OnClientConnected(ulong clientID) {
            // If we haven't saved the main scene yet, do it.
            if (_activeScene == null) {
                _activeScene = new NetworkScene {
                                                  scene = SceneManager.GetActiveScene(),
                                                  sceneName = SceneManager.GetActiveScene().name,
                                                  sceneID = GetSceneID()
                                              };
            }

            // Send to the current scene.
            var changePacket = new SceneChangePacket
                               {sceneIndex = GetSceneIndex(_activeScene.sceneName), sceneNetID = _activeScene.sceneID};
            NetworkServer.instance.Send(clientID, changePacket, DefaultChannels.ReliableSequenced);
        }

        #endregion

        #region Scene Fetching

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static int GetSceneIndex(Scene scene) {
            if (_activeScene.scene == scene) {
                return 0;
            }

            for (var i = 0; i < _subScenesList.Count; i++) {
                if (_subScenesList[i].scene == scene)
                    return i;
            }

            // Not loaded.
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static uint GetSceneNetID(Scene scene) {
            if (_activeScene.scene == scene) {
                return _activeScene.sceneID;
            }

            for (var i = 0; i < _subScenesList.Count; i++) {
                if (_subScenesList[i].scene == scene)
                    return _subScenesList[i].sceneID;
            }

            // Not loaded.
            throw new Exception("Scene could not be found! Are you sure it is loaded?");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public static NetworkScene GetNetScene(int index = 0) {
            if (index == 0)
                return _activeScene;

            if (index < _subScenesList.Count)
                return _subScenesList[index];
            return null;
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