using System;
using System.Collections.Generic;
using System.Linq;
using FNNLib.Backend;
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
        internal static void SpawnObjectLocally(NetworkIdentity identity, ulong networkID, bool playerObject,
                                                ulong? ownerClientID) {
            if (identity == null)
                throw new ArgumentNullException("Cannot spawn with null identity!");
            
            if (identity.isSpawned)
                throw new Exception("Already spawned!");

            identity.isSpawned = true;
            identity.networkID = networkID;

            identity._ownerClientID = ownerClientID;
            identity.isPlayerObject = playerObject;

            spawnedObjects.Add(identity.networkID, identity);
            spawnedObjectsList.Add(identity);

            if (ownerClientID != null) {
                if (NetworkManager.instance.isServer) {
                    if (playerObject)
                        NetworkManager.instance.connectedClients[ownerClientID.Value].playerObject = identity;
                    else NetworkManager.instance.connectedClients[ownerClientID.Value].ownedObjects.Add(identity);
                } else if (playerObject && ownerClientID.Value == NetworkManager.instance.localClientID) {
                    NetworkManager.instance.connectedClients[ownerClientID.Value].playerObject = identity;
                }
            }
            
            if (NetworkManager.instance.isServer) {
                for (var i = 0; i < NetworkManager.instance.connectedClientsList.Count; i++) {
                    // TODO: Initial delegate to determine if client should observe
                    identity.observers.Add(NetworkManager.instance.connectedClientsList[i].clientID);
                }
            }
            
            // TODO: Network functions like NetworkSpawn etc.
        }
        
        // Run on client only
        internal static NetworkIdentity CreateObjectLocal(ulong prefabHash, ulong? parentNetID, Vector3? position, Quaternion? rotation) {
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
                        null;
                } else {
                    NetworkManager.instance.connectedClients[spawnedObjects[networkID].ownerClientID].ownedObjects
                                  .RemoveAll((identity) => identity.networkID == networkID);
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
            
            var netObj = CreateObjectLocal(packet.prefabHash, parentNetID, position, rotation);
            SpawnObjectLocally(netObj, packet.networkID, packet.isPlayerObject, packet.ownerClientID);
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
                packet.sceneID = identity.sceneID.GetValueOrDefault(0);
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
    }
}