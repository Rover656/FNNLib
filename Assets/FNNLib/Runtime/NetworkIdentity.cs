using System.Collections.Generic;
using UnityEngine;

namespace FNNLib {
    [AddComponentMenu("Networking/Network Identity")]
    public class NetworkIdentity : MonoBehaviour {
        /// <summary>
        /// The scene that this object is in.
        /// Will always equal 0 if scene management is disabled.
        /// </summary>
        public ulong sceneID { get; private set; }
        
        /// <summary>
        /// The network ID of this object in the scene.
        /// </summary>
        public ulong networkID { get; private set; }
        
        /// <summary>
        /// Whether or not the object is spawned on the network.
        /// </summary>
        public bool isSpawned { get; internal set; }

        /// <summary>
        /// The client ID of the owner.
        /// </summary>
        public ulong ownerClientID {
            get {
                if (_ownerClientID == null)
                    return 0;
                return _ownerClientID.Value;
            }

            internal set {
                if (NetworkManager.instance != null && value == 0)
                    _ownerClientID = null;
                else _ownerClientID = value;
            }
        }

        internal ulong? _ownerClientID;
        
        /// <summary>
        /// Whether or not this object is the player object for a client.
        /// </summary>
        public bool isPlayerObject { get; internal set; }

        /// <summary>
        /// Whether or not this is the local player's player object.
        /// </summary>
        public bool isLocalPlayer => NetworkManager.instance != null && isPlayerObject &&
                                     ownerClientID == NetworkManager.instance.localClientID;

        /// <summary>
        /// Whether or not the local player has ownership over this object.
        /// </summary>
        public bool isOwner =>
            NetworkManager.instance != null && ownerClientID == NetworkManager.instance.localClientID;

        public bool isOwnedByServer => NetworkManager.instance != null && ownerClientID == 0;
        
        #region Observers

        /// <summary>
        /// This object's observers.
        /// </summary>
        internal List<ulong> observers = new List<ulong>();

        public void AddObserver(ulong clientID) {
            if (!observers.Contains(clientID))
                observers.Add(clientID);
        }

        public void RemoveObserver(ulong clientID) {
            if (observers.Contains(clientID))
                observers.Remove(clientID);
        }

        public bool IsObserving(ulong clientID) {
            return observers.Contains(clientID);
        }

        #endregion
        
        #region Spawning

        public void Spawn() {
            
        }

        public void SpawnAsPlayerObject(ulong clientID) {
            
        }

        public void UnSpawn() {
            
        }

        #endregion
        
        #region Ownership

        public void ChangeOwnership(ulong newOwnerClientID) {
            
        }
        
        public void RemoveOwnership() {
            
        }

        #endregion
    }
}