using System;
using FNNLib.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FNNLib {
    // TODO: This will be fleshed out once object spawning and scene management is done.
    public class NetworkBehaviour : MonoBehaviour {
        #region Identity Fetch
        
        public NetworkIdentity identity {
            get {
                if (_identity == null)
                    _identity = GetComponentInParent<NetworkIdentity>();
                if (_identity == null)
                    throw new NullReferenceException("Failed to get NetworkIdentity for NetworkBehaviour!");
                return _identity;
            }
        }
        
        private NetworkIdentity _identity;

        public bool hasIdentity {
            get {
                if (_identity == null)
                    _identity = GetComponentInParent<NetworkIdentity>();
                return _identity != null;
            }
        }
        
        #endregion
        
        #region Identity Passthrough

        public uint networkSceneID => identity.networkSceneID;

        public NetworkScene networkScene => identity.networkScene;

        public ulong networkID => identity.networkID;

        public bool isSpawned => identity.isSpawned;

        public bool isLocalPlayer => identity.isLocalPlayer;

        public ulong ownerClientID => identity.ownerClientID;

        public bool isOwner => identity.isOwner;

        public bool isOwnedByServer => identity.isOwnedByServer;
        
        /// <summary>
        /// Is running in a server context?
        /// </summary>
        public bool isServer => NetworkManager.instance.isServer;

        /// <summary>
        /// Is running in a client context?
        /// </summary>
        public bool isClient => NetworkManager.instance.isClient;
        
        #endregion
        
        private void Awake() {
            // Ensure we have an identity. Alert if we don't
            if (!hasIdentity)
                Debug.LogError("NetworkBehaviour attached to \"" + name + " \" could not find a NetworkIdentity in its parent!");
        }
        
        #region Scene Manager helpers

        public GameObject NetInstantiate(GameObject go, Vector3 position, Quaternion rotation) {
            return networkScene.Instantiate(go, position, rotation);
        }
        
        #endregion
    }
}