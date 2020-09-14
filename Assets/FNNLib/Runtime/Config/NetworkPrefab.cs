using System;
using UnityEngine;

namespace FNNLib.Config {
    [Serializable]
    public class NetworkPrefab {
        /// <summary>
        /// Get the prefab hash from the network identity.
        /// </summary>
        internal ulong prefabHash {
            get {
                if (prefab == null) {
                    return 0;
                } else if (prefab.GetComponent<NetworkIdentity>() == null) {
                    return 0;
                } else {
                    return prefab.GetComponent<NetworkIdentity>().prefabHash;
                }
            }
        }

        /// <summary>
        /// The prefab.
        /// </summary>
        public GameObject prefab;
    }
}