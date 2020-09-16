using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FNNLib.SceneManagement {
    /// <summary>
    /// NetworkScene controls the scene's network presence, and the presence of its network objects.
    /// </summary>
    public class NetworkScene {
        /// <summary>
        /// The name of the scene
        /// </summary>
        public string name { get; internal set; }
        
        /// <summary>
        /// This scene's ID.
        /// </summary>
        public uint netID { get; internal set; }
        
        /// <summary>
        /// The client loading mode.
        /// If this is single and server mode is additive, this will be offset in space.
        /// </summary>
        public LoadSceneMode serverMode { get; internal set; }

        /// <summary>
        /// The client loading mode.
        /// If this is single and server mode is additive, this will be offset in space.
        /// </summary>
        public LoadSceneMode clientMode { get; internal set; }

        /// <summary>
        /// The scene that we are controlling.
        /// </summary>
        public Scene scene { get; internal set; }
        
        /// <summary>
        /// The packing offset given to this scene by the NetworkSceneManager.
        /// </summary>
        public Vector3 packingOffset { get; internal set; }

        /// <summary>
        /// This scene's observers.
        /// </summary>
        internal readonly List<ulong> observers = new List<ulong>();

        #region Observers (scene members)

        internal void AddObserver(ulong clientID) {
            // Add to observer list and spawn all current objects for the client (if they can see them)
            observers.Add(clientID);
        }

        internal void RemoveObserver(ulong clientID) {
            // Remove from observer list, they no longer recieve updates.
            // We don't tell the client to despawn objects however, as this function is only called if they disconnect or load a different scene.
            observers.Remove(clientID);
        }
        
        #endregion
        
        #region Objects
        
        public GameObject Instantiate(GameObject go, Vector3 position, Quaternion rotation) {
            var created = Object.Instantiate(go, position, rotation);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }
        
        // TODO: Moving networked objects...

        /// <summary>
        /// Find all objects of type in a scene. Can only search for monobehaviours
        /// ***VERY*** slow on a server or host if you have many scenes.
        /// Should only be slightly slower on clients. You're better to use FindObjectsOfType if a client is only on 1 scene, but remember the host!
        /// </summary>
        /// <param name="sceneID"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T[] FindObjectsOfType<T>() where T : MonoBehaviour {
            var allObjects = Object.FindObjectsOfType<T>().ToList();
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