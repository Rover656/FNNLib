using System;
using System.Collections.Generic;
using System.Linq;
using FNNLib.Messaging;
using FNNLib.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FNNLib.Spawning {
    /// <summary>
    /// The spawn manager controls the sync of network identities across the network.
    /// </summary>
    public static class NewSpawnManager {
        #region Identity Spawn Management

        /// <summary>
        /// Dictionary containing every spawned object.
        /// </summary>
        internal static readonly Dictionary<ulong, NetworkIdentity> spawnedIdentities = new Dictionary<ulong, NetworkIdentity>();

        /// <summary>
        /// List of all spawned identities. Useful for iterations which delete.
        /// </summary>
        private static readonly List<NetworkIdentity> spawnedIdentitiesList = new List<NetworkIdentity>();

        /// <summary>
        /// Buffered packets.
        /// </summary>
        internal static readonly PacketBufferCollection<ulong> identityPacketBuffer = new PacketBufferCollection<ulong>();
        
        #region Common

        internal static void SpawnIdentityLocal(NetworkIdentity identity, ulong networkID, bool isSceneObject, bool isPlayerObject, ulong? ownerID) {
            if (identity == null)
                throw new ArgumentNullException();
            if (identity.isSpawned)
                throw new Exception();
            
            // Configure identity
            identity.isSpawned = true;
            identity.networkID = networkID;
            identity.isSceneObject = isSceneObject;
            identity.isPlayerObject = isPlayerObject;
            identity._ownerClientID = ownerID;
            
            // Add to spawned identities list
            spawnedIdentities.Add(identity.networkID, identity);
            spawnedIdentitiesList.Add(identity);

            // Store ownership TODO: Deal with multiple player objects!
            if (ownerID != null) {
                if (isPlayerObject) {
                    NetworkManager.instance.connectedClients[ownerID.Value].playerObject = identity.networkID;
                } else if (NetworkManager.instance.isServer) {
                    NetworkManager.instance.connectedClients[ownerID.Value].ownedObjects.Add(identity.networkID);
                }
            }
            
            // Process observers
            if (NetworkManager.instance.isServer) {
                var scene = identity.networkScene;
                foreach (var client in NetworkManager.instance.connectedClientsList) {
                    if (!scene.observers.Contains(client.clientID))
                        continue;

                    // TODO: Custom IsVisible delegate
                    
                    identity.observers.Add(client.clientID);
                }
            }
            
            // Network starts
            identity.ResetNetStartInvoked();
            identity.InvokeBehaviourNetStart();
            
            // Execute any pending packets for this identity.
            while (identityPacketBuffer.HasPending(networkID) && spawnedIdentities.ContainsKey(networkID)) {
                identityPacketBuffer.ExecutePending(networkID);
            }
            identityPacketBuffer.DestroyQueue(networkID);
        }

        internal static void DestroyIdentity(ulong identityID, bool destroyObject) {
            if (spawnedIdentities.ContainsKey(identityID))
                DestroyIdentity(spawnedIdentities[identityID], destroyObject);
        }

        internal static void DestroyIdentity(NetworkIdentity identity, bool destroyObject) {
            // Deal with leftover ownerships
            if (!identity.isOwnedByServer && NetworkManager.instance.connectedClients.ContainsKey(identity.ownerClientID)) {
                if (identity.isPlayerObject) {
                    NetworkManager.instance.connectedClients[identity.ownerClientID].playerObject = 0;
                } else {
                    NetworkManager.instance.connectedClients[identity.ownerClientID].ownedObjects
                                  .RemoveAll((id) => id == identity.networkID);
                }
            }
            
            // Mark as not spawned
            identity.isSpawned = false;

            // Destroy the object
            if (destroyObject && identity.gameObject != null) {
                Object.Destroy(identity.gameObject);
            }
            
            // Remove from stored lists
            spawnedIdentities.Remove(identity.networkID);
            spawnedIdentitiesList.Remove(identity);
        }
        
        #endregion
        
        #region Server
        
        internal static void ServerSendSpawn(ulong clientID, NetworkIdentity identity) {
            NetworkChannel.ReliableSequenced.ServerSend(clientID, SpawnObjectPacket.Create(identity));
        }

        /// <summary>
        /// Send a spawn call for an identity to a list of observers
        /// </summary>
        /// <param name="observers"></param>
        /// <param name="identity"></param>
        internal static void ServerSendSpawn(List<ulong> observers, NetworkIdentity identity) {
            NetworkChannel.ReliableSequenced.ServerSend(observers, SpawnObjectPacket.Create(identity));
        }
        
        #endregion
        
        #region Client

        /// <summary>
        /// Handle a spawn packet.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="packet"></param>
        internal static void ClientHandleSpawnPacket(NetworkChannel channel, SpawnObjectPacket packet) {
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
            
            // Create and spawn
            var netObj = CreateLocalObject(packet.isSceneObject ?? true, packet.networkedInstanceID, packet.sceneID, packet.prefabHash,
                                           parentNetID, position, rotation);
            SpawnIdentityLocal(netObj, packet.networkID, packet.isSceneObject ?? true, packet.isPlayerObject, packet.ownerClientID);
        }

        /// <summary>
        /// Handle a destroy packet.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="packet"></param>
        internal static void ClientHandleDestroyPacket(NetworkChannel channel, DestroyObjectPacket packet) {
            DestroyIdentity(packet.networkID, true);
        }

        /// <summary>
        /// Create network identity's object locally.
        /// </summary>
        /// <param name="isSceneObject"></param>
        /// <param name="instanceID"></param>
        /// <param name="sceneID"></param>
        /// <param name="prefabHash"></param>
        /// <param name="parentNetID"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static NetworkIdentity CreateLocalObject(bool isSceneObject, ulong instanceID, uint sceneID, ulong prefabHash, ulong? parentNetID, Vector3? position, Quaternion? rotation) {
            // Ensure that this scene is loaded
            var client = NetworkManager.instance.connectedClients[NetworkManager.instance.localClientID];
            if (!client.loadedScenes.Contains(sceneID))
                throw new Exception("Cannot create object for scene that isn't loaded!");

            NetworkIdentity parent = null;
            if (parentNetID.HasValue) {
                if (spawnedIdentities.ContainsKey(parentNetID.Value)) {
                    parent = spawnedIdentities[parentNetID.Value];
                } else throw new Exception("Unable to find parent!");
            }

            if (isSceneObject) {
                // Get the pending scene object
                if (!pendingSceneIdentities.ContainsKey(instanceID)) {
                    throw new Exception("Failed to find pending scene identity matching packet.");
                }

                // Get the identity
                var identity = pendingSceneIdentities[instanceID];
                pendingSceneIdentities.Remove(instanceID);
                
                // Apply parent if present.
                if (parent != null)
                    identity.transform.SetParent(parent.transform, true);
                
                return identity;
            }

            var prefabIdx = GetNetworkedPrefabIndex(prefabHash);
            if (prefabIdx < 0)
                throw new Exception("Unrecognized prefab hash!");

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
        
        #endregion

        #endregion
        
        #region Scene Management Interop

        #region Server side

        /// <summary>
        /// Call when the server loaded a scene.
        /// </summary>
        /// <param name="scene"></param>
        internal static void ServerLoadScene(NetworkScene scene) {
            // Get all network identities to be spawned. We want the scene objects.
            var objects = scene.FindObjectsOfType<NetworkIdentity>();
            
            // Spawn scene objects
            foreach (var identity in objects.Where(identity => identity.isSceneObject == null)) {
                SpawnIdentityLocal(identity, GetNetworkID(), true, false, null);
            }
        }

        /// <summary>
        /// Call when the server unloaded a scene.
        /// </summary>
        /// <param name="scene"></param>
        internal static void ServerUnloadScene(NetworkScene scene) {
            // Destroy all spawned identities for this scene.
            for (var i = spawnedIdentitiesList.Count; i >= 0; i--) {
                if (spawnedIdentitiesList[i].networkSceneID != scene.netID) continue;
                // We don't destroy the object because the scene is being unloaded, so its done for us.
                DestroyIdentity(spawnedIdentitiesList[i], false);
            }
        }

        /// <summary>
        /// Call (on server) when a client joins a scene
        /// </summary>
        /// <param name="client"></param>
        /// <param name="scene"></param>
        /// <exception cref="Exception"></exception>
        internal static void ServerOnClientJoinScene(ulong client, NetworkScene scene) {
            // Ensure the client is in this scene
            if (!scene.observers.Contains(client))
                throw new Exception();
            
            // TODO: IsVisible delegate here too
            foreach (var identityPair in spawnedIdentities.Where(pair => pair.Value.networkScene == scene)) {
                identityPair.Value.AddObserver(client);
            }
        }

        internal static void ServerOnClientLeaveScene(ulong client, NetworkScene scene) {
            // TODO: Delete client from all observer lists.
        }

        /// <summary>
        /// JUSTIN: Fixes the issue of scene objects not being spawned.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="loadMode"></param>
        internal static void ServerSpawnOnSceneLoad(Scene scene, LoadSceneMode loadMode) {
            // Spawn scene objects.
            ServerLoadScene(NetworkSceneManager.GetNetScene(scene));
        }
        
        #endregion
        
        #region Client side
        
        private static readonly Dictionary<ulong, NetworkIdentity> pendingSceneIdentities =
            new Dictionary<ulong, NetworkIdentity>();

        /// <summary>
        /// Fired when the client loads a scene.
        /// This will gather all scene objects and prepare them for spawn calls.
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="additive"></param>
        internal static void ClientLoadScene(NetworkScene scene, bool additive) {
            // Clear if we're not loading additively
            if (!additive)
                pendingSceneIdentities.Clear();
            
            // Get scene network identities
            var identities = scene.FindObjectsOfType<NetworkIdentity>();
            
            // Register these as pending, ready for their spawn calls
            foreach (var identity in identities.Where(identity => identity.isSceneObject == null)) {
                pendingSceneIdentities.Add(identity.sceneInstanceID, identity);
            }
        }
        
        #endregion
        
        #endregion
        
        #region Unfinished/Incomplete/Old APIs
        
        // TODO: Added while I test all the new stuff above. I wanna tidy/rename.
        internal static void ServerUnspawnAllSceneObjects() {
            for (var i = spawnedIdentitiesList.Count - 1; i >= 0; i--) {
                if (spawnedIdentitiesList[i].isSceneObject != null || spawnedIdentitiesList[i].isSceneObject == true) {
                    DestroyIdentity(spawnedIdentitiesList[i], false);
                }
            }
        }

        internal static void DestroyNonSceneObjects() {
            for (var i = spawnedIdentitiesList.Count - 1; i >= 0; i--) {
                if (spawnedIdentitiesList[i].isSceneObject == null || spawnedIdentitiesList[i].isSceneObject == false) {
                    DestroyIdentity(spawnedIdentitiesList[i], true);
                }
            }
        }
        
        #region Network Ids
        
        // TODO: Some form of ID system? Used across scenes, identities etc. Also make extensible for the end user/dev.

        private static ulong _idCounter;
        
        internal static ulong GetNetworkID() {
            return _idCounter++;
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
        
        #endregion
    }
}