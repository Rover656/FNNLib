using System;
using System.Collections.Generic;
using FNNLib.Backend;
using FNNLib.SceneManagement;
using FNNLib.Spawning;
using FNNLib.Transports;
using FNNLib.Utilities;
using UnityEngine;

namespace FNNLib {
    [AddComponentMenu("Networking/Network Identity")]
    public class NetworkIdentity : MonoBehaviour {
        /// <summary>
        /// The scene that this object is in.
        /// </summary>
        public uint networkSceneID => networkScene.netID;

        /// <summary>
        /// The network scene this object resides in.
        /// </summary>
        public NetworkScene networkScene {
            get {
                if (NetworkManager.instance == null)
                    return null;
                return NetworkSceneManager.GetNetScene(gameObject.scene);
            }
        }
        
        /// <summary>
        /// The network ID of this object in the scene.
        /// </summary>
        public ulong networkID { get; internal set; }
        
        /// <summary>
        /// Whether or not the object is spawned on the network.
        /// </summary>
        public bool isSpawned { get; internal set; }
        
        /// <summary>
        /// Whether or not this object was spawned with the scene.
        /// </summary>
        public bool? isSceneObject { get; internal set; }

        /// <summary>
        /// Unique identifier for this object in the scene.
        /// Only used if this is a scene object.
        /// Do not set this value yourself!
        /// </summary>
        [HideInInspector]
        public ulong sceneInstanceID;

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

        private void OnValidate() => ValidateHash();
        
        #region Behaviours

        private List<NetworkBehaviour> _behaviours;

        public List<NetworkBehaviour> behaviours {
            get {
                if (_behaviours == null) {
                    _behaviours = new List<NetworkBehaviour>(GetComponentsInChildren<NetworkBehaviour>(true));
                }
                return _behaviours;
            }
        }
        
        #endregion

        #region Prefabs
        
        /// <summary>
        /// The prefab hash generator
        /// </summary>
        public string prefabHashGenerator;

        [HideInInspector]
        public ulong prefabHash;

        private void ValidateHash() {
            if (string.IsNullOrEmpty(prefabHashGenerator))
                prefabHashGenerator = name;
            prefabHash = prefabHashGenerator.GetStableHash64();
        }
        
        #endregion
        
        #region Observers

        /// <summary>
        /// This object's observers.
        /// </summary>
        internal List<ulong> observers = new List<ulong>();

        public void AddObserver(ulong clientID) {
            if (!isSpawned)
                throw new Exception("Must be spawned before observers can be added!");
            if (!NetworkManager.instance.isServer)
                throw new Exception("Only the server can add observers!");
            if (!observers.Contains(clientID)) {
                observers.Add(clientID);
                SpawnManager.ServerSendSpawnCall(clientID, this);
            }
        }

        public void RemoveObserver(ulong clientID) {
            if (!isSpawned)
                throw new Exception("Must be spawned before observers can be removed!");
            if (!NetworkManager.instance.isServer)
                throw new Exception("Only the server can remove observers!");
            if (observers.Contains(clientID))
                observers.Remove(clientID);
            
            var destroyPacket = new DestroyObjectPacket {networkID = networkID};
                    
            // Send to all, so that even if someone is instructed to create it, they will destroy it after.
            NetworkServer.instance.Send(clientID, destroyPacket, DefaultChannels.ReliableSequenced);
        }

        public bool IsObserving(ulong clientID) {
            return observers.Contains(clientID);
        }

        #endregion
        
        #region Spawning

        public void Spawn() {
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Spawn may only be called by the server!");
            SpawnManager.SpawnObjectLocally(this, SpawnManager.GetNetworkID(), false, false, null);
            SpawnManager.ServerSendSpawnCall(observers, this);
        }

        public void SpawnWithOwnership(ulong clientID) {
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Spawn may only be called by the server!");
            SpawnManager.SpawnObjectLocally(this, SpawnManager.GetNetworkID(), false, false, clientID);
            SpawnManager.ServerSendSpawnCall(observers, this);
        }

        public void SpawnAsPlayerObject(ulong clientID) {
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Spawn may only be called by the server!");
            SpawnManager.SpawnObjectLocally(this, SpawnManager.GetNetworkID(), false, true, clientID);
            SpawnManager.ServerSendSpawnCall(observers, this);
        }

        public void UnSpawn() {
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("Spawn may only be called by the server!");
            if (isSpawned)
                SpawnManager.OnDestroy(networkID, false);
        }

        #endregion
        
        #region Ownership

        public void ChangeOwnership(ulong newOwnerClientID) {
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("ChangeOwnership may only be called by the server!");
            //todo
        }
        
        public void RemoveOwnership() {
            if (!NetworkManager.instance.isServer)
                throw new NotSupportedException("RemoveOwnership may only be called by the server!");
            //todo
        }

        #endregion
        
        #region Internal Events

        internal void ResetNetStartInvoked() {
            foreach (var behaviour in behaviours) {
                behaviour.netStartInvoked = false;
            }
        }

        internal void InvokeBehaviourNetStart() {
            foreach (var behaviour in behaviours) {
                if (!behaviour.netStartInvoked) {
                    if (!behaviour.internalNetStartInvoked) {
                        behaviour.InternalNetworkStart();
                        behaviour.internalNetStartInvoked = true;
                    }
                    
                    behaviour.NetworkStart();
                    behaviour.netStartInvoked = true;
                }
            }
        }
        
        #endregion
        
        #region Lifecycle

        private void OnDestroy() {
            if (NetworkManager.instance != null) {
                SpawnManager.OnDestroy(networkID, false);
            }
        }

        #endregion
    }
}