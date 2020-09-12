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
        private readonly List<ulong> _observers = new List<ulong>();

        #region Networked Objects
        
        // TODO: Spawning player objects and for only specific observers.

        /// <summary>
        /// Spawn a networked object using its identity.
        /// </summary>
        /// <param name="netIdentity"></param>
        internal void SpawnForAll(NetworkIdentity netIdentity) {
            // TODO
        }

        /// <summary>
        /// Despawns an object using its identity.
        /// Generally you won't need to call this, it will be handled by the Identity on destruction.
        /// Only do this if you want the object to be removed from clients only.
        /// </summary>
        /// <param name="netIdentity"></param>
        internal void DespawnForAll(NetworkIdentity netIdentity) {
            // TODO, Remove the object from all observers.
        }
        
        #endregion

        #region Observers (scene members)

        internal void AddObserver(ulong clientID) {
            // Add to observer list and spawn all current objects for the client (if they can see them)
            _observers.Add(clientID);
        }

        internal void RemoveObserver(ulong clientID) {
            // Remove from observer list, they no longer recieve updates.
            // We don't tell the client to despawn objects however, as this only happens if they disconnect or load a different scene.
            _observers.Remove(clientID);
        }
        
        #endregion
    }
}