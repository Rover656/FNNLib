using System;
using System.Collections.Generic;
using FNNLib.Messaging;
using FNNLib.SceneManagement;
using FNNLib.Transports;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FNNLib.Spawning {
    public static class SpawnManager {
        /// <summary>
        /// Objects that have been spawned
        /// </summary>
        public static readonly Dictionary<ulong, NetworkIdentity> spawnedObjects =
            new Dictionary<ulong, NetworkIdentity>();

        public static readonly List<NetworkIdentity> spawnedObjectsList = new List<NetworkIdentity>();

        private static readonly Dictionary<ulong, NetworkIdentity> pendingSceneObjects =
            new Dictionary<ulong, NetworkIdentity>();
        
        /// <summary>
        /// Buffered RPC calls.
        /// </summary>
        internal static readonly PacketBufferCollection<ulong> networkObjectPacketBuffer = new PacketBufferCollection<ulong>();

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
                    else
                        NetworkManager.instance.connectedClients[ownerClientID.Value].ownedObjects
                                      .Add(identity.networkID);
                }
                else if (playerObject && ownerClientID.Value == NetworkManager.instance.localClientID) {
                    NetworkManager.instance.connectedClients[ownerClientID.Value].playerObject = identity.networkID;
                }
            }

            if (NetworkManager.instance.isServer) {
                for (var i = 0; i < NetworkManager.instance.connectedClientsList.Count; i++) {
                    var clientID = NetworkManager.instance.connectedClientsList[i].clientID;
                    // If the client isn't observing the scene, don't make them observe this object.
                    if (!NetworkSceneManager.GetNetScene(identity.networkSceneID).observers.Contains(clientID))
                        continue;

                    // TODO: Custom is observer callback for more customisation

                    identity.observers.Add(clientID);
                }
            }

            identity.ResetNetStartInvoked();
            identity.InvokeBehaviourNetStart();
            
            // Execute pending packets while the object hasn't been destroyed (in case we get a destroy packet)
            while (networkObjectPacketBuffer.HasPending(networkID) && spawnedObjects.ContainsKey(networkID)) {
                networkObjectPacketBuffer.ExecutePending(networkID);
            }
            networkObjectPacketBuffer.DestroyQueue(networkID);
        }

        // Run on client only
        internal static NetworkIdentity CreateObjectLocal(bool isSceneObject, ulong instanceID, uint sceneID,
                                                          ulong prefabHash, ulong? parentNetID, Vector3? position,
                                                          Quaternion? rotation) {
            // Check that the scene ID matches the client's current scene.
            if (!NetworkManager.instance.isServer) {
                var client = NetworkManager.instance.connectedClients[NetworkManager.instance.localClientID];
                if (!client.loadedScenes.Contains(sceneID)) {
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

            if (isSceneObject) {
                // See if we have this object pending
                if (!pendingSceneObjects.ContainsKey(instanceID)) {
                    Debug.LogError("Client did not expect this instance to be spawned.");
                    return null;
                }

                var obj = pendingSceneObjects[instanceID];
                pendingSceneObjects.Remove(instanceID);

                if (parent != null)
                    obj.transform.SetParent(parent.transform, true);

                return obj;
            } else {
                int prefabIdx = GetNetworkedPrefabIndex(prefabHash);
                if (prefabIdx < 0)
                    return null;

                var prefab = NetworkManager.instance.networkConfig.networkedPrefabs[prefabIdx].prefab;

                var sceneToCreateIn = NetworkSceneManager.GetNetScene(sceneID);

                var createdObject = (position != null || rotation != null)
                                        ? sceneToCreateIn.Instantiate(prefab, position.GetValueOrDefault(),
                                                                      rotation.GetValueOrDefault())
                                                         .GetComponent<NetworkIdentity>()
                                        : sceneToCreateIn.Instantiate(prefab).GetComponent<NetworkIdentity>();

                if (parent != null)
                    createdObject.transform.SetParent(parent.transform, true);

                return createdObject;
            }

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
                }
                else {
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
                    NetworkManager.instance.ServerSendToAll(destroyPacket, DefaultChannels.ReliableSequenced);
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

        internal static void ClientHandleSpawnPacket(SpawnObjectPacket packet, int channel) {
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
            

            var netObj = CreateObjectLocal(packet.isSceneObject ?? true, packet.networkedInstanceID, packet.sceneID, packet.prefabHash,
                                           parentNetID, position, rotation);
            SpawnObjectLocally(netObj, packet.networkID, packet.isSceneObject ?? true, packet.isPlayerObject, packet.ownerClientID);
        }

        internal static void ClientHandleDestroy(DestroyObjectPacket packet, int channel) {
            OnDestroy(packet.networkID, true);
        }

        #endregion

        #region Server

        internal static void ServerSendSpawnCall(ulong clientID, NetworkIdentity identity) {
            NetworkManager.instance.ServerSend(clientID, CreateSpawnObjectPacket(identity), DefaultChannels.ReliableSequenced);
        }

        internal static void ServerSendSpawnCall(List<ulong> observers, NetworkIdentity identity) {
            NetworkManager.instance.ServerSend(observers, CreateSpawnObjectPacket(identity),
                                               DefaultChannels.ReliableSequenced);
        }

        private static SpawnObjectPacket CreateSpawnObjectPacket(NetworkIdentity identity) {
            // Create and write packet data
            var packet = new SpawnObjectPacket();

            packet.isPlayerObject = identity.isPlayerObject;

            packet.networkID = identity.networkID;
            packet.ownerClientID = identity.ownerClientID;
            packet.sceneID = identity.networkSceneID;

            if (identity.transform.parent != null) {
                var parent = identity.transform.parent.GetComponent<NetworkIdentity>();
                packet.hasParent = parent != null;
                if (packet.hasParent)
                    packet.parentNetID = parent.networkID;
            }
            else packet.hasParent = false;

            packet.isSceneObject = identity.isSceneObject;
            packet.networkedInstanceID = identity.sceneInstanceID;
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

        #region Scenes

        //Spawns any NetworkIdentities in the scene at scene start.
        internal static void ServerSpawnSceneObjects(uint sceneID = 0) {
            // Get all networked objects for this scene.
            var objects = NetworkSceneManager.GetNetScene(sceneID).FindObjectsOfType<NetworkIdentity>();

            // Spawn any scene objects
            foreach (var obj in objects) {
                if (obj.isSceneObject == null) {
                    SpawnObjectLocally(obj, GetNetworkID(), true, false, null);
                }
            }
        }

        internal static void ServerUnspawnSceneObjects(uint sceneID) {
            for (var i = spawnedObjectsList.Count - 1; i >= 0; i--) {
                if (spawnedObjectsList[i].isSceneObject != null && spawnedObjectsList[i].isSceneObject == true && spawnedObjectsList[i].networkSceneID == sceneID) {
                    // Don't destroy on server, no point. The scene is about to be unloaded.
                    OnDestroy(spawnedObjectsList[i].networkID, false);
                }
            }
        }

        internal static void ServerUnspawnAllSceneObjects() {
            for (var i = spawnedObjectsList.Count - 1; i >= 0; i--) {
                if (spawnedObjectsList[i].isSceneObject != null && spawnedObjectsList[i].isSceneObject == true) {
                    // Don't destroy on server, no point. The scene is about to be unloaded.
                    OnDestroy(spawnedObjectsList[i].networkID, false);
                }
            }
        }

        internal static void DestroyNonSceneObjects() {
            for (var i = spawnedObjectsList.Count - 1; i >= 0; i--) {
                if (spawnedObjectsList[i].isSceneObject == null || spawnedObjectsList[i].isSceneObject == false) {
                    OnDestroy(spawnedObjectsList[i].networkID, true);
                }
            }
        }
        
        internal static void DestroySceneObjects(uint sceneID) {
            for (var i = spawnedObjectsList.Count - 1; i >= 0; i--) {
                if (spawnedObjectsList[i].networkSceneID != sceneID) continue;
                if (spawnedObjectsList[i].isSceneObject == null || spawnedObjectsList[i].isSceneObject == false) {
                    OnDestroy(spawnedObjectsList[i].networkID, true);
                }
            }
        }

        internal static void ClientCollectSceneObjects(uint sceneID, bool loadedAdditively) {
            // Clear, we're going again
            if (!loadedAdditively)
                pendingSceneObjects.Clear();
            
            // Get all networked objects for this scene.
            var objects = NetworkSceneManager.GetNetScene(sceneID).FindObjectsOfType<NetworkIdentity>();

            // Spawn any scene objects
            foreach (var obj in objects) {
                if (obj.isSceneObject == null) {
                    // Save in pending list
                    pendingSceneObjects.Add(obj.sceneInstanceID, obj);
                }
            }
        }

        internal static void ClientResetSceneObjects() {
            for (var i = spawnedObjectsList.Count - 1; i >= 0; i++) {
                if (spawnedObjectsList[i].isSceneObject != null && spawnedObjectsList[i].isSceneObject == true) {
                    // Don't destroy on server, no point. The scene is about to be unloaded.
                    OnDestroy(spawnedObjectsList[i].networkID, false);
                }
            }
        }

        internal static void OnClientJoinScene(ulong clientID, uint sceneID) {
            // Check the player is observing the scene they are joining
            if (!NetworkSceneManager.GetNetScene(sceneID).observers.Contains(clientID))
                throw new
                    NotSupportedException("Will not spawn objects for client that isn't in the scene it requested.");

            // TODO: IsVisible delegate for custom handling.
            foreach (var obj in spawnedObjectsList) {
                if (obj.networkSceneID == sceneID)
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