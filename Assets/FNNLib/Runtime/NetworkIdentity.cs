using UnityEngine;

namespace FNNLib {
    // TODO: Devise a way that a developer can implement custom spawning, because currently you are forced to use the NetworkSceneManager.
    //        The solution will likely be public functions that permit the setting of information in the network identity so that an implementation can be made.
    [AddComponentMenu("Networking/Network Identity")]
    public class NetworkIdentity : MonoBehaviour {
        /// <summary>
        /// The scene that this object is in.
        /// </summary>
        public uint sceneID { get; private set; }
        
        /// <summary>
        /// The network ID of this object in the scene.
        /// </summary>
        public uint networkID { get; private set; }

        /// <summary>
        /// Is running in a server context?
        /// </summary>
        public bool isServer => NetworkManager.instance.isServer;

        /// <summary>
        /// Is running in a client context?
        /// </summary>
        public bool isClient => NetworkManager.instance.isClient;
    }
}