using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FNNLib.Exceptions;
using FNNLib.Messaging;
using FNNLib.Spawning;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace FNNLib.SceneManagement {
    // TODO: TIDY
    [Serializable]
    public struct NetworkableScene {
        public string sceneName;
        public ScenePackingData scenePackingData;
    }
    
    public static class NetworkSceneManager {
        #region Events
        
        #region Server side
        
        public static UnityEvent<NetworkScene, LoadSceneMode, LoadSceneMode> serverLoadedScene;
        public static UnityEvent<NetworkScene> serverUnloadedScene;
        
        // TODO: The next two need new packets defining...
        public static UnityEvent<NetworkScene, ulong, LoadSceneMode, LoadSceneMode> serverClientLoadedScene;
        
        public static UnityEvent<NetworkScene, ulong> serverClientUnloadedScene;
        
        #endregion
        
        #region Client side
        
        /// <summary>
        /// Fires just after the scene has successfully loaded.
        /// </summary>
        public static UnityEvent<NetworkScene, LoadSceneMode> clientLoadedScene;
        
        /// <summary>
        /// Fires right before the unloading process begins.
        /// </summary>
        public static UnityEvent<NetworkScene> clientUnloadedScene;
        
        #endregion
        
        #endregion
        
        #region Initialization

        /// <summary>
        /// Initialise the scene manager.
        /// </summary>
        internal static void Init() {
            if (NetworkManager.instance.isServer) {
                // Attach scene load event.
                SceneManager.sceneLoaded += ServerOnSceneLoad;
                
                // Register the initial scene.
                RegisterInitialScene();
            }
        }

        /// <summary>
        /// Scene manager shutdown
        /// </summary>
        internal static void Shutdown() {
            if (NetworkManager.instance.isServer) {
                SceneManager.sceneLoaded -= ServerOnSceneLoad;
            }
        }

        /// <summary>
        /// Register the initial/starting scene.
        /// </summary>
        private static void RegisterInitialScene() {
            // Change scene if we have an initial target
            var initialScene = NetworkManager.instance.networkConfig.initialScene;
            var sceneChanged = false;
            if (!string.IsNullOrEmpty(initialScene)) {
                // TODO: Security

                if (SceneManager.GetActiveScene().name != initialScene) {
                    SceneManager.LoadScene(initialScene);
                    sceneChanged = true;
                }
            }
            
            // Get the scene
            var scene = SceneManager.GetActiveScene();
            
            // Return if this scene is already registered
            if (loadedScenes.Any(loaded => loaded.Value.scene == scene)) {
                return;
            }
            
            // Generate an ID
            var netID = GetSceneID();
            
            // Create scene object
            var netScene = new NetworkScene(scene, netID, LoadSceneMode.Single, LoadSceneMode.Single);
            
            // Add to scene list
            loadedScenes.TryAdd(netID, netScene);
            
            // Add to main scenes
            _mainScenes.Add(netScene);
            
            // Fire load scene event for existing scenes.
            if (!sceneChanged) {
                SpawnManager.ServerLoadScene(netScene);
            }
        }
        
        #endregion

        #region Scene Management
        
        /// <summary>
        /// All loaded scenes.
        /// </summary>
        internal static readonly ConcurrentDictionary<uint, NetworkScene> loadedScenes = new ConcurrentDictionary<uint, NetworkScene>();

        /// <summary>
        /// The list of scenes that everyone should have loaded.
        /// </summary>
        private static readonly List<NetworkScene> _mainScenes = new List<NetworkScene>();

        /// <summary>
        /// Loads a scene with the given mode for everyone.
        /// This scene will be a "main" scene if loadForAll is true, meaning its loaded on all clients.
        /// If loadForAll is false, nobody will load the scene, you will be expected to do that yourself.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="mode"></param>
        /// <param name="loadForAll"></param>
        /// <returns></returns>
        public static NetworkScene LoadScene(string sceneName, LoadSceneMode mode, bool loadForAll = true) {
            return LoadScene(sceneName, mode, mode, loadForAll ? NetworkManager.instance.allClientIDs : null,
                             loadForAll);
        }

        /// <summary>
        /// Loads a scene with the given mode for the clients provided.
        /// This scene will not be a "main" scene.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="mode"></param>
        /// <param name="clientsToLoadFor"></param>
        /// <returns></returns>
        public static NetworkScene LoadScene(string sceneName, LoadSceneMode mode, List<ulong> clientsToLoadFor) {
            return LoadScene(sceneName, mode, mode, clientsToLoadFor,
                             false);
        }

        /// <summary>
        /// Loads a scene with the given modes for everyone.
        /// This scene will be a "main" scene if loadForAll is true, meaning its loaded on all clients.
        /// If loadForAll is false, nobody will load the scene, you will be expected to do that yourself.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="serverMode"></param>
        /// <param name="clientMode"></param>
        /// <param name="loadForAll"></param>
        /// <returns></returns>
        public static NetworkScene LoadScene(string sceneName, LoadSceneMode serverMode, LoadSceneMode clientMode,
                                                bool loadForAll = true) {
            return LoadScene(sceneName, serverMode, clientMode, loadForAll ? NetworkManager.instance.allClientIDs : null,
                             loadForAll);
        }

        /// <summary>
        /// Loads a scene with the given mode for the clients provided.
        /// This scene will not be a "main" scene.
        /// </summary>
        /// <param name="sceneName"></param>
        /// <param name="serverMode"></param>
        /// <param name="clientMode"></param>
        /// <param name="clientsToLoadFor"></param>
        /// <returns></returns>
        public static NetworkScene LoadScene(string sceneName, LoadSceneMode serverMode, LoadSceneMode clientMode,
                                                List<ulong> clientsToLoadFor) {
            return LoadScene(sceneName, serverMode, clientMode, clientsToLoadFor, false);
        }
        
        /// <summary>
        /// Load a scene on the server and selected clients.
        ///
        /// The scene will be loaded on all clients if clientMode is single.
        /// If the client mode is not single, you must either use the clientsToLoadFor parameter, or load the scene for clients afterwards for yourself. 
        /// </summary>
        private static NetworkScene LoadScene(string sceneName, LoadSceneMode serverMode, LoadSceneMode clientMode,
                                                 List<ulong> clientsToLoadFor, bool isMainScene) {
            if (!NetworkManager.instance.isServer)
                throw new NotServerException();
            if (serverMode == LoadSceneMode.Single && clientMode == LoadSceneMode.Additive)
                throw new InvalidOperationException();
            if (serverMode == LoadSceneMode.Single && clientsToLoadFor != null)
                throw new
                    NotSupportedException("If you load a scene in single mode on the server, all clients must be moved there. Pass null instead.");
            if (!NetworkScene.CanSendClientTo(sceneName))
                throw new NotSupportedException();
            if (serverMode == LoadSceneMode.Additive && clientMode == LoadSceneMode.Single && !NetworkManager.instance.networkConfig.enableHybridScenes)
                throw new NotSupportedException("Hybrid scenes are not enabled!");
            if (serverMode == LoadSceneMode.Additive && clientMode == LoadSceneMode.Single)
                throw new NotImplementedException(); // Need to implement scene packing.
            
            // Safety check to stop my stupidity
            if (isMainScene && clientsToLoadFor == null)
                throw new Exception("YOURE STUPID. DONT DO THAT!");
            
            // Load the scene on the server
            SceneManager.LoadScene(sceneName, serverMode); // TODO: Shall we do this Async as to not hold server process?
            
            // Generate a network ID
            var netID = GetSceneID();
            
            // Create scene object
            var netScene = new NetworkScene(SceneManager.GetSceneAt(SceneManager.sceneCount - 1), netID, serverMode,
                                               clientMode);
            loadedScenes.TryAdd(netID, netScene); // TODO: Need to check for failures probably.

            // Load for people we are asked to.
            if (clientsToLoadFor != null) {
                netScene.LoadFor(clientsToLoadFor);
            }

            // Main scenes.
            if (isMainScene) {
                if (clientMode == LoadSceneMode.Single)
                    _mainScenes.Clear();
                _mainScenes.Add(netScene);
            }

            return netScene;
        }

        public static AsyncOperation UnloadSceneAsync(NetworkScene scene, NetworkScene fallbackScene) {
            if (!NetworkManager.instance.isServer)
                throw new NotServerException();
            if (!scene.isLoaded)
                throw new Exception();
            if (loadedScenes.Count <= 1)
                throw new NotSupportedException();
            if (!fallbackScene.isLoaded)
                throw new InvalidOperationException();
            
            // Remove from main scenes
            if (_mainScenes.Contains(scene))
                _mainScenes.Remove(scene);
            
            // Remove from dictionary
            loadedScenes.TryRemove(scene.ID, out _);
            
            // Deal with the observers
            foreach (var observer in scene.observers) {
                // Tell the observers to load the fallback if it isn't
                if (!NetworkManager.instance.connectedClients[observer].loadedScenes.Contains(fallbackScene)) {
                    fallbackScene.LoadFor(observer);
                }
                
                // Remove this scene from the observer's lists
                NetworkManager.instance.connectedClients[observer].loadedScenes.Remove(scene);
            }
            
            // Send unload to all observers
            var unloadPacket = new SceneUnloadPacket {sceneID = scene.ID};
            NetworkChannel.ReliableSequenced.ServerSend(scene.observers, unloadPacket);

            // Unload scene objects
            SpawnManager.ServerUnloadScene(scene);
            
            // Unload scene
            var op = SceneManager.UnloadSceneAsync(scene.scene);
            op.completed += _ => Resources.UnloadUnusedAssets();
            return op;
        }

        public static NetworkScene GetScene(uint sceneID) {
            return loadedScenes.ContainsKey(sceneID) ? loadedScenes[sceneID] : null;
        }

        public static NetworkScene GetScene(Scene scene) {
            return (from subScene in loadedScenes where subScene.Value.scene == scene select subScene.Value).FirstOrDefault();
        }
        
        #endregion
        
        #region Game Objects

        public static void MoveNetworkObjectToScene(NetworkIdentity identity, NetworkScene scene) {
            if (!NetworkManager.instance.isServer)
                throw new NotServerException();
            if (!scene.isLoaded)
                throw new Exception();
            
            // Move the object
            SceneManager.MoveGameObjectToScene(identity.gameObject, scene.scene);
            
            // Tell observers to move the object
            var movePacket = new MoveObjectToScenePacket {
                                                             networkID = identity.networkID,
                                                             destinationScene = scene.ID
                                                         };
            
            // Send an appropriate packet to each observer
            for (var i = identity.observers.Count - 1; i >= 0; i--) {
                var observer = identity.observers[i];
                
                if (scene.observers.Contains(observer)) {
                    NetworkChannel.ReliableSequenced.ServerSend(observer, movePacket);
                } else {
                    identity.RemoveObserver(observer);
                }
            }
            
            // Send spawn packets to people in the scene that have not yet seen it
            foreach (var observer in scene.observers) {
                if (!identity.observers.Contains(observer))
                    identity.AddObserver(observer);
            }
        }
        
        #endregion
        
        #region Networking
        
        internal static readonly PacketBufferCollection<uint> bufferedScenePackets = new PacketBufferCollection<uint>();
        
        #region Client

        internal static void ClientHandleSceneLoadPacket(NetworkChannel channel, SceneLoadPacket packet) {
            // Get the scene to load
            var scene = NetworkManager.instance.networkConfig.networkableScenes[packet.sceneIndex];

            // Async load scene
            var asyncLoad = SceneManager.LoadSceneAsync(scene.sceneName, packet.mode);
            var loadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            asyncLoad.completed += op => {
                                       // Add to loaded scenes list
                                       var netScene = new NetworkScene(loadedScene, packet.sceneNetworkID,
                                                                          LoadSceneMode.Additive, packet.mode); // Server mode doesn't matter.
                                       loadedScenes.TryAdd(packet.sceneNetworkID, netScene);

                                       // Prepare scene objects
                                       SpawnManager.ClientLoadScene(netScene, packet.mode == LoadSceneMode.Additive);

                                       // Add to the loaded scenes list of the client
                                       NetworkManager.instance.connectedClients[NetworkManager.instance.localClientID]
                                                     .loadedScenes.Add(netScene);

                                       // Run buffered actions (spawns)
                                       while (bufferedScenePackets.HasPending(packet.sceneNetworkID)) {
                                           bufferedScenePackets.ExecutePending(packet.sceneNetworkID);
                                       }

                                       bufferedScenePackets.DestroyQueue(packet.sceneNetworkID);
                                       
                                       // Invoke client scene load event
                                       clientLoadedScene?.Invoke(netScene, packet.mode);
                                   };
        }

        internal static void ClientHandleSceneUnloadPacket(NetworkChannel channel, SceneUnloadPacket packet) {
            if (loadedScenes.ContainsKey(packet.sceneID)) {
                // Get scene
                var scene = GetScene(packet.sceneID);
                
                // Invoke event
                clientUnloadedScene?.Invoke(scene);
                
                // Remove from the loaded scenes list of the client
                NetworkManager.instance.connectedClients[NetworkManager.instance.localClientID]
                              .loadedScenes.Remove(scene);
                loadedScenes.TryRemove(packet.sceneID, out _);
                
                // Perform the unload
                var op = SceneManager.UnloadSceneAsync(scene.scene);
                op.completed += _ => Resources.UnloadUnusedAssets();
            }
        }

        internal static void ClientHandleMoveObjectPacket(NetworkChannel channel, MoveObjectToScenePacket packet) {
            // Get the object and scene
            var identity = SpawnManager.spawnedIdentities[packet.networkID];
            var scene = GetScene(packet.destinationScene);
            SceneManager.MoveGameObjectToScene(identity.gameObject, scene.scene);
        }

        #endregion
        
        #region Server

        internal static void ServerOnClientConnected(ulong clientID) {
            // Load all main scenes on the client.
            for (var i = 0; i < _mainScenes.Count; i++) {
                var scene = _mainScenes[i];
                var loadFallbackPacket = new SceneLoadPacket {
                                                                 sceneNetworkID = scene.ID,
                                                                 mode =
                                                                     i == 0 ? LoadSceneMode.Single : scene.clientLoadMode,
                                                                 sceneIndex = NetworkScene.GetSceneIndex(scene.sceneName)
                                                             };
                NetworkChannel.ReliableSequenced.ServerSend(clientID, loadFallbackPacket);

                // Add to observers list
                scene.observers.Add(clientID);

                // Add to the loaded scenes list of the client
                NetworkManager.instance.connectedClients[clientID].loadedScenes.Add(scene);

                // Spawn scene for player
                SpawnManager.ServerOnClientJoinScene(clientID, scene);
            }
        }
        
        #endregion
        
        #endregion
        
        #region Spawn Manager Interop
        
        #region Server
        
        /// <summary>
        /// JUSTIN: Fixes the issue of scene objects not being spawned.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="loadMode"></param>
        private static void ServerOnSceneLoad(Scene scene, LoadSceneMode loadMode) {
            // Spawn scene objects.
            var netScene = GetScene(scene);
            SpawnManager.ServerLoadScene(netScene);

            // Fire event
            serverLoadedScene?.Invoke(netScene, netScene.clientLoadMode, netScene.serverLoadMode);
        }
        
        #endregion
        
        #endregion
        
        #region Stuff I wanna change
        
        private static uint _sceneIDCounter;

        private static uint GetSceneID() {
            _sceneIDCounter++;
            return _sceneIDCounter;
        }
        
        #endregion
    }
}