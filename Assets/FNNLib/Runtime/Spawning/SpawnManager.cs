using System;
using System.Collections.Generic;
using System.Linq;
using FNNLib.Backend;
using FNNLib.SceneManagement;
using FNNLib.Transports;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FNNLib.Spawning {
    // TODO: Look at https://github.com/MidLevel/MLAPI/blob/88bdf8b372cee16d49a30c5786de8a151b928b2c/MLAPI-Editor/PostProcessScene.cs#L37
    //       This shows how MLAPI sorts all scene objects to generate unique instance ID's for non-prefab's in a scene.

    public static class SpawnManager {
        /// <summary>
        /// Objects that have been spawned
        /// </summary>
        public static readonly Dictionary<ulong, NetworkIdentity> spawnedObjects =
            new Dictionary<ulong, NetworkIdentity>();

        public static readonly List<NetworkIdentity> spawnedObjectsList = new List<NetworkIdentity>();

        #region Spawning

        /// <summary>
        /// Spawns the object locally.
        /// This configures the network identity ready for networked behaviours to start.
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="networkID"></param>
        /// <param name="playerObject"></param>
        /// <param name="ownerClientID"></param>
        internal static void SpawnObjectLocally(NetworkIdentity identity, ulong networkID, bool sceneObject,
                                                bool playerObject, ulong? ownerClientID) {
            if (identity == null)
                throw new ArgumentNullException("Cannot spawn with null identity!");
            if (identity.isSpawned)
                throw new Exception("Already spawned!");

            identity.isSpawned = true;

            identity.isSceneObject = sceneObject;
            identity.networkID = networkID;

            identity._ownerClientID = ownerClientID;
            identity.isPlayerObject = playerObject;

            spawnedObjects.Add(identity.networkID, identity);
            spawnedObjectsList.Add(identity);
            
            if (ownerClientID != null) {
                if (NetworkManager.instance.isServer) {
                    if (playerObject)
                        NetworkManager.instance.connectedClients[ownerClientID.Value].playerObject = identity.networkID;
                    else NetworkManager.instance.connectedClients[ownerClientID.Value].ownedObjects.Add(identity.networkID);
                } else if (playerObject && ownerClientID.Value == NetworkManager.instance.localClientID) {
                    NetworkManager.instance.connectedClients[ownerClientID.Value].playerObject = identity.networkID;
                }
            }
            
            if (NetworkManager.instance.isServer) {
                for (var i = 0; i < NetworkManager.instance.connectedClientsList.Count; i++) {
                    var clientID = NetworkManager.instance.connectedClientsList[i].clientID;
                    if (NetworkManager.instance.networkConfig.useSceneManagement) {
                        // If the client isn't observing the scene, don't make them observe this object.
                        if (!NetworkSceneManager.GetNetScene(identity.sceneID).observers.Contains(clientID))
                            continue;
                    }
                    
                    // TODO: Custom is observer callback for more customisation
                    
                    identity.observers.Add(clientID);
                }
            }
            
            // TODO: Network functions like NetworkSpawn etc.
        }
        
        // Run on client only
        internal static NetworkIdentity CreateObjectLocal(ulong sceneID, ulong prefabHash, ulong? parentNetID, Vector3? position, Quaternion? rotation) {
            // Check that the scene ID matches the client's current scene.
            if (NetworkManager.instance.networkConfig.useSceneManagement && !NetworkManager.instance.isServer) {
                var client = NetworkManager.instance.connectedClients[NetworkManager.instance.localClientID];
                if (client.sceneID != sceneID) {
                    Debug.LogWarning("Cannot spawn object from another scene. Ignoring");
                    return null;
                }
            }
            
            NetworkIdentity parent = null;
            if (parentNetID.HasValue) {
                if (spawnedObjects.ContainsKey(parentNetID.Value)) {
                    parent = spawnedObjects[parentNetID.Value];
                }
                else Debug.LogWarning("Failed to find parent!");
            }

            if (NetworkManager.instance.networkConfig.useSceneManagement) {
                int prefabIdx = GetNetworkedPrefabIndex(prefabHash);
                if (prefabIdx < 0)
                    return null;

                var prefab = NetworkManager.instance.networkConfig.networkedPrefabs[prefabIdx].prefab;

                var createdObject = (position != null || rotation != null)
                                        ? Object.Instantiate(prefab, position.GetValueOrDefault(),
                                                             rotation.GetValueOrDefault())
                                                .GetComponent<NetworkIdentity>()
                                        : Object.Instantiate(prefab).GetComponent<NetworkIdentity>();

                if (parent != null)
                    createdObject.transform.SetParent(parent.transform, true);

                return createdObject;
            }

            // TODO: Non-scene manager impl if we keep the useSceneManagement flag.

            return null;
        }

        #endregion
        
        #region Despawning

        internal static void OnDestroy(ulong networkID, bool destroyObject) {
            if (NetworkManager.instance == null)
                return;
            
            // If it has been removed already (or does not exist) do nothing
            if (!spawnedObjects.ContainsKey(networkID))
                return;
            
            // If this object is owned by someone, remove it from the list
            if (!spawnedObjects[networkID].isOwnedByServer &&
                NetworkManager.instance.connectedClients.ContainsKey(spawnedObjects[networkID].ownerClientID)) {
                if (spawnedObjects[networkID].isPlayerObject) {
                    NetworkManager.instance.connectedClients[spawnedObjects[networkID].ownerClientID].playerObject =
                        0;
                } else {
                    NetworkManager.instance.connectedClients[spawnedObjects[networkID].ownerClientID].ownedObjects
                                  .RemoveAll((id) => id == networkID);
                }
            }
            
            // Mark as not spawned
            spawnedObjects[networkID].isSpawned = false;

            // Destroy for clients TODO: Release ID
            if (NetworkManager.instance != null && NetworkManager.instance.isServer) {
                if (spawnedObjects[networkID] != null) {
                    var destroyPacket = new DestroyObjectPacket {networkID = networkID};
                    
                    // Send to all, so that even if someone is instructed to create it, they will destroy it after.
                    NetworkServer.instance.SendToAll(destroyPacket, DefaultChannels.ReliableSequenced);
                }
            }
            
            // Get the gameobject and destroy if if we are supposed to
            if (destroyObject && spawnedObjects[networkID].gameObject != null) {
                Object.Destroy(spawnedObjects[networkID].gameObject);
            }

            spawnedObjects.Remove(networkID);
            spawnedObjectsList.RemoveAll((identity) => identity.networkID == networkID);
        }
        
        #endregion

        #region Client Handlers

        internal static void ClientHandleSpawnPacket(ulong sender, SpawnObjectPacket packet) {
            // Make nullable vars
            ulong? parentNetID = null;
            if (packet.hasParent)
                parentNetID = packet.parentNetID;

            Vector3? position = null;
            Quaternion? rotation = null;
            if (packet.includesTransform) {
                position = packet.position;
                rotation = Quaternion.Euler(packet.eulerRotation);
            }
            
            // Whether or not this is a scene object
            bool sceneObject;
            if (NetworkManager.instance.networkConfig.useSceneManagement) {
                sceneObject = false;
            } else {
                sceneObject = packet.isSceneObject ?? true;
            }
            
            var netObj = CreateObjectLocal(packet.sceneID, packet.prefabHash, parentNetID, position, rotation);
            SpawnObjectLocally(netObj, packet.networkID, sceneObject, packet.isPlayerObject, packet.ownerClientID);
        }

        internal static void ClientHandleDestroy(ulong sender, DestroyObjectPacket packet) {
            OnDestroy(packet.networkID, true);
        }

        #endregion
        
        #region Server
        
        internal static void ServerSendSpawnCall(ulong clientID, NetworkIdentity identity) {
            NetworkServer.instance.Send(clientID, CreateSpawnObjectPacket(identity), DefaultChannels.ReliableSequenced);
        }

        internal static void ServerSendSpawnCall(List<ulong> observers, NetworkIdentity identity) {
            NetworkServer.instance.Send(observers, CreateSpawnObjectPacket(identity), DefaultChannels.ReliableSequenced);
        }

        private static SpawnObjectPacket CreateSpawnObjectPacket(NetworkIdentity identity) {
            // Create and write packet data
            var packet = new SpawnObjectPacket();

            packet.isPlayerObject = identity.isPlayerObject;

            packet.networkID = identity.networkID;
            packet.ownerClientID = identity.ownerClientID;
            if (NetworkManager.instance.networkConfig.useSceneManagement) {
                packet.sceneID = identity.sceneID;
            }

            if (identity.transform.parent != null) {
                var parent = identity.transform.parent.GetComponent<NetworkIdentity>();
                packet.hasParent = parent != null;
                if (packet.hasParent)
                    packet.parentNetID = parent.networkID;
            } else packet.hasParent = false;

            packet.isSceneObject = false;// TODO
            packet.networkedInstanceID = 0; // TODO
            packet.prefabHash = identity.prefabHash;

            // TODO: Allow not sending transform on spawn

            packet.includesTransform = true;
            packet.position = identity.transform.position;
            packet.eulerRotation = identity.transform.rotation.eulerAngles;
            return packet;
        }

        internal static void OnClientConnected(ulong clientID) {
            // If scene management is disabled, spawn objects for the new client.
            if (!NetworkManager.instance.networkConfig.useSceneManagement)
                OnClientJoinScene(clientID, 0);
        }
        
        #endregion

        #region Prefabs

        public static int GetNetworkedPrefabIndex(ulong hash) {
            for (var i = 0; i < NetworkManager.instance.networkConfig.networkedPrefabs.Count; i++) {
                if (NetworkManager.instance.networkConfig.networkedPrefabs[i].prefabHash == hash)
                    return i;
            }

            return -1;
        }

        #endregion
        
        #region Network IDs
        
        // TODO: ID release system

        private static ulong _networkIDCounter;
        internal static ulong GetNetworkID() {
            _networkIDCounter++;
            return _networkIDCounter;
        }
        
        #endregion
        
        #region Scenes

        /// <summary>
        /// Spawns any NetworkIdentities in the scene at scene start.
        /// </summary>
        internal static void SpawnSceneObjects() {
            
        }

        internal static void ResetSceneObjects() {
            
        }

        internal static void DestroySpawnedSceneObjects() {
            
        }

        internal static void OnClientJoinScene(ulong clientID, uint sceneID) {
            // Check the player is observing the scene they are joining
            if (NetworkManager.instance.networkConfig.useSceneManagement) {
                if (!NetworkSceneManager.GetNetScene(sceneID).observers.Contains(clientID))
                    throw new NotSupportedException("Will not spawn objects for client that isn't in the scene it requested.");
            }

            // TODO: IsVisible delegate for custom handling.
            foreach (var obj in spawnedObjectsList) {
                if (!NetworkManager.instance.networkConfig.useSceneManagement || obj.sceneID == sceneID)
                    obj.AddObserver(clientID);
            }
        }

        /// <summary>
        /// Removes anything the client owned in this scene.
        /// </summary>
        /// <param name="clientID"></param>
        internal static void OnClientChangeScene(ulong clientID) {
            // Destroy's anything the client owned and removes the client from every observer list.
            foreach (var obj in spawnedObjectsList) {
                obj.observers.Remove(clientID);
                if (obj.ownerClientID == clientID)
                    OnDestroy(obj.networkID, true);
            }
        }
        
        #endregion
    }
}