using System;
using System.Collections.Generic;
using System.Linq;
using FNNLib.Exceptions;
using FNNLib.Messaging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FNNLib.SceneManagement {
    /// <summary>
    /// The new network scene class.
    /// This takes more control over scene control.
    /// </summary>
    public class NetworkScene {
        public Scene scene { get; internal set; }

        public string sceneName => scene.name;
        
        /// <summary>
        /// The scene's network ID
        /// </summary>
        public uint ID { get; internal set; }

        /// <summary>
        /// The client loading mode.
        /// If this is single and server mode is additive, this will be offset in space.
        /// </summary>
        public LoadSceneMode serverLoadMode { get; internal set; }

        /// <summary>
        /// The client loading mode.
        /// If this is single and server mode is additive, this will be offset in space.
        /// </summary>
        public LoadSceneMode clientLoadMode { get; internal set; }
        
        /// <summary>
        /// Whether the scene is still loaded in the network scene manager.
        /// </summary>
        public bool isLoaded => NetworkSceneManager.loadedScenes.ContainsKey(ID);

        internal NetworkScene(Scene scene, uint id, LoadSceneMode serverLoad, LoadSceneMode clientLoad) {
            this.scene = scene;
            this.ID = id;
            this.serverLoadMode = serverLoad;
            this.clientLoadMode = clientLoad;
        }

        #region Networked Loads

        /// <summary>
        /// The list of observing clients.
        /// </summary>
        internal List<ulong> observers = new List<ulong>();

        /// <summary>
        /// Tell the client to load this scene.
        /// </summary>
        /// <param name="clientID"></param>
        public void LoadFor(ulong clientID) {
            if (!NetworkManager.instance.isServer)
                throw new NotServerException();
            // TODO: This should never happen:
            if (NetworkSceneManager.loadedScenes.ContainsKey(ID))
                throw new Exception("This scene is no longer loaded in the scene manager...");
            
            // Add to observers
            observers.Add(clientID);
            
            // Send the load packet
            var loadPacket = new SceneLoadPacket {
                                                     sceneNetworkID = ID,
                                                     mode = clientLoadMode,
                                                     sceneIndex = GetSceneIndex(scene.name)
                                                 };
            NetworkChannel.ReliableSequenced.ServerSend(clientID, loadPacket);
            
            // Add to loaded scenes list
            NetworkManager.instance.connectedClients[clientID].loadedScenes.Add(this);
        }

        /// <summary>
        /// Tell these clients to load this scene.
        /// </summary>
        /// <param name="clientIDs"></param>
        public void LoadFor(List<ulong> clientIDs) {
            if (!NetworkManager.instance.isServer)
                throw new NotServerException();
            // TODO: This should never happen:
            if (!NetworkSceneManager.loadedScenes.ContainsKey(ID))
                throw new Exception("This scene is no longer loaded in the scene manager...");
            
            // Add to observers
            observers.AddRange(clientIDs);
            
            // Send the load packet
            var loadPacket = new SceneLoadPacket {
                                                     sceneNetworkID = ID,
                                                     mode = clientLoadMode,
                                                     sceneIndex = GetSceneIndex(scene.name)
                                                 };
            NetworkChannel.ReliableSequenced.ServerSend(clientIDs, loadPacket);
            
            // Add to loaded scenes list
            foreach (var clientID in clientIDs)
                NetworkManager.instance.connectedClients[clientID].loadedScenes.Add(this);
        }

        // TODO: We probably need fallback scenes for these:
        public void UnloadFor(ulong clientID) {
            if (!NetworkManager.instance.isServer)
                throw new NotServerException();
        }

        public void UnloadFor(List<ulong> clientIDs) {
            if (!NetworkManager.instance.isServer)
                throw new NotServerException();
        }
        
        #endregion
        
        #region Identities
        
        #endregion
        
        #region Security

        internal static bool CanSendClientTo(string sceneName) {
            return NetworkManager.instance.networkConfig.networkableScenes.FindIndex((networkableScene) =>
                       networkableScene.sceneName == sceneName) != -1;
        }

        internal static int GetSceneIndex(string sceneName) {
            return NetworkManager.instance.networkConfig.networkableScenes.FindIndex((networkableScene) =>
                networkableScene.sceneName == sceneName);
        }
        
        #endregion
        
        #region Old compat

        public GameObject Instantiate(GameObject go) {
            return Instantiate(go, Vector3.zero, Quaternion.identity);
        }
        
        public GameObject Instantiate(GameObject go, Vector3 position, Quaternion rotation) {
            // NOTE: This can be used on the client to spawn things, but they still cannot be spawned on the server.
            var created = UnityEngine.Object.Instantiate(go, position, rotation);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        /// <summary>
        /// Find all objects of type in a scene. Can only search for monobehaviours
        /// ***VERY*** slow on a server or host if you have many scenes.
        /// Should only be slightly slower on clients. You're better to use FindObjectsOfType if a client is only on 1 scene, but remember the host!
        /// </summary>
        /// <param name="sceneID"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T[] FindObjectsOfType<T>() where T : MonoBehaviour {
            var allObjects = UnityEngine.Object.FindObjectsOfType<T>().ToList();
            var sceneObjects = new List<T>();
            foreach (var obj in allObjects) {
                if (obj.gameObject.scene == scene)
                    sceneObjects.Add(obj);
            }

            return sceneObjects.ToArray();
        }
        
        #endregion
    }
}