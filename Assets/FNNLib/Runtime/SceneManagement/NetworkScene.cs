using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FNNLib.SceneManagement {
    /// <summary>
    /// NetworkScene controls the scene's network presence, and the presence of its network objects.
    /// </summary>
    public class NetworkScene {
        /// <summary>
        /// The name of the scene
        /// </summary>
        public string sceneName { get; internal set; }
        
        /// <summary>
        /// This scene's ID.
        /// </summary>
        public uint sceneID { get; internal set; }

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
    }
}