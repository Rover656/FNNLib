﻿using FNNLib.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FNNLib.SceneManagement {
    /// <summary>
    /// NetworkScene controls the scene's network presence, and the presence of its network objects.
    /// </summary>
    public class NetworkScene {
        /// <summary>
        /// The scene packing data for this scene.
        /// </summary>
        public ScenePackingData data;
        
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
        /// Spawn a networked object using its identity.
        /// </summary>
        /// <param name="netIdentity"></param>
        public void Spawn(NetworkIdentity netIdentity) {
            // TODO
        }

        // TODO: Render the packing data gizmo for the scene.
    }
}