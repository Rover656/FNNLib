﻿using System;
using System.Collections.Concurrent;
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
    /// This is used on 
    /// </summary>
    public static class NetworkSceneManager {
        /// <summary>
        /// The current scene (if not using subscenes).
        /// </summary>
        private static NetworkScene _serverCurrentScene;

        /// <summary>
        /// Whether or not sub scenes are being used.
        /// </summary>
        private static bool _usingSubScenes;
        
        /// <summary>
        /// List of currently active networked scenes (if using subscenes).
        /// </summary>
        private static ConcurrentDictionary<uint, NetworkScene> _networkedScenes;

        public static void ServerLoadScene(string sceneName) {
            if (!NetworkManager.instance.useSceneManagement)
                throw new NotSupportedException("The NetworkSceneManager is not enabled by the current NetworkManager!");
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server may load scenes!");
            if (_usingSubScenes)
                throw new NotSupportedException("Cannot use ServerLoadScene when subscenes are active. Use subscene methods instead!");
            if (!CanSendClientTo(sceneName))
                throw new NotSupportedException("Cannot send client to this scene. It is not on the permitted scenes list.");
            
            // Load the scene on the server
            SceneManager.LoadScene(sceneName);
            var scene = SceneManager.GetActiveScene();
            
            // Get network ID (we use build indices for network IDs in this mode as the same scene cannot be duplicated. This is to stop old packets spawning stuff into the world.)
            // TODO: Maybe make it so you explicitly enable subscenes instead of enabling when you load one so ID problems don't occur.
            var netID = Convert.ToUInt32(scene.buildIndex);
            
            // Set the current scene
            _serverCurrentScene = new NetworkScene {scene = scene, sceneID = netID, sceneName = sceneName};
            
            // Send the scene change packet
            var changePacket = new SceneChangePacket {sceneIndex = GetSceneIndex(sceneName), sceneNetID = netID};
            NetworkServer.instance.SendToAll(changePacket);
        }
        
        /// <summary>
        /// Load a sub scene on the server side ready for accepting clients.
        /// This will use the packing data in the networked scene to spawn it in a free location.
        /// TODO: Have a root scene that the server will live in.
        /// </summary>
        /// <param name="sceneName">The scene to be loaded.</param>
        public static void ServerLoadSubScene(string sceneName) {
            if (!NetworkManager.instance.useSceneManagement)
                throw new NotSupportedException("The NetworkSceneManager is not enabled by the current NetworkManager!");
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Only the server may load scenes!");
            if (!CanSendClientTo(sceneName))
                throw new NotSupportedException("Cannot send client to this scene. It is not on the permitted scenes list.");
            
            throw new NotImplementedException("Subscenes will be revisited once the regular scene system works. This is so I can ensure the system works before adding more advanced features...");
            
            // We are now entering subscene mode TODO: Deal with the existing scene... need to work out if its the "host" or "root" scene.
            _usingSubScenes = true;

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
        public static void ServerUnloadSubScene(string sceneName, uint fallbackScene) {
            if (!NetworkManager.instance.useSceneManagement)
                throw new NotSupportedException("The NetworkSceneManager is not enabled by the current NetworkManager!");
            // TODO
        }

        public static void SendClientTo(ulong clientID, string sceneName) {
            if (!NetworkManager.instance.useSceneManagement)
                throw new NotSupportedException("The NetworkSceneManager is not enabled by the current NetworkManager!");
            // TODO: Will this only be used for subscenes (i.e. games with more than 1 scene at a time). Probably.
            // TODO: Move the client to the scene.
        }

        private static bool CanSendClientTo(string sceneName) {
            return NetworkManager.instance.permittedScenes.FindIndex((networkableScene) =>
                                                                         networkableScene.sceneName == sceneName) != -1;
        }

        private static int GetSceneIndex(string sceneName) {
            return NetworkManager.instance.permittedScenes.FindIndex((networkableScene) =>
                                                                         networkableScene.sceneName == sceneName);
        }

        private static ScenePackingData GetScenePackingData(string sceneName) {
            return NetworkManager.instance.permittedScenes.Find((networkableScene) =>
                                                                    networkableScene.sceneName == sceneName)
                                 .scenePackingData;
        }
        
        #region Client Handlers

        internal static void ClientHandleSceneChangePacket(ulong sender, SceneChangePacket packet) {
            // TODO: Change scene
            /*
             * The process:
             * - If we're already on the scene, simply clear any networked objects so we don't duplicate any.
             * - Start a transition period (some form of waiting state where the game doesn't freeze, but nothing is happening)
             * - Change scene first (we don't have to save networked objects, because they will be spawned by the server once we confirm the scene change was successful)
             * - Once the scene has changed, tell the server that the scene change was successful, then the server will begin sending us networked objects.
             */
        }
        
        #endregion
        
        #region Server Handlers

        internal static void OnClientConnected(ulong clientID) {
            return; // TODO: We need to get the current scene first.
            if (_usingSubScenes) {
                // TODO: We need starting scene config
            } else {
                // Send to the current scene.
                var changePacket = new SceneChangePacket {sceneIndex = GetSceneIndex(_serverCurrentScene.sceneName), sceneNetID = Convert.ToUInt32(_serverCurrentScene.scene.buildIndex)};
                NetworkServer.instance.Send(clientID, changePacket);
            }
        }
        
        #endregion
    }
}