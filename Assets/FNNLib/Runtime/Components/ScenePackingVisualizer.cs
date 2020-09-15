using System;
using FNNLib.SceneManagement;
using UnityEngine;

namespace FNNLib.Components {
    public class ScenePackingVisualizer : MonoBehaviour {
        public ScenePackingData packingData;
        private void OnDrawGizmos() {
            Gizmos.color = Color.red;
            if (packingData != null)
                Gizmos.DrawWireCube(new Vector3(0, 0, 0), packingData.sceneMinimumBoundary);
        }
    }
}