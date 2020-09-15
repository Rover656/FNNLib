using UnityEngine;

namespace FNNLib.SceneManagement {
    [CreateAssetMenu(fileName = "ScenePacking.asset", menuName = "Networking/Scene Packing Data", order = 0)]
    public class ScenePackingData : ScriptableObject {
        /// <summary>
        /// The scene bounds.
        /// This configures how the NetworkSceneManager adds additional scenes and where.
        /// </summary>
        public Vector3 sceneMinimumBoundary;
    }
}