using FNNLib.Messaging;
using FNNLib.SceneManagement;
using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.Spawning {
    /// <summary>
    /// Packet that spawns an object on the client.
    /// </summary>
    [ClientPacket]
    internal class SpawnObjectPacket : ISerializable, IBufferablePacket {
        /// <summary>
        /// Is this object controlled by a player.
        /// </summary>
        public bool isPlayerObject;

        /// <summary>
        /// The network ID of the object.
        /// </summary>
        public ulong networkID;
        
        /// <summary>
        /// The ID of the network object's owner.
        /// </summary>
        public ulong ownerClientID;
        
        /// <summary>
        /// The ID of the scene the object is in.
        /// </summary>
        public uint sceneID;

        /// <summary>
        /// Whether or not the object has a parent.
        /// </summary>
        public bool hasParent;
        
        /// <summary>
        /// The network ID of the object's parent.
        /// </summary>
        public ulong parentNetID;

        /// <summary>
        /// Whether or not the object is a scene object.
        /// </summary>
        public bool? isSceneObject;
        
        /// <summary>
        /// Networked instance ID (scene objects)
        /// </summary>
        public ulong networkedInstanceID;
        
        /// <summary>
        /// Prefab hash for newly spawned objects.
        /// </summary>
        public ulong prefabHash;

        /// <summary>
        /// Whether or not this packet contains a transform.
        /// </summary>
        public bool includesTransform;
        
        /// <summary>
        /// The position of the object.
        /// </summary>
        public Vector3 position;
        
        /// <summary>
        /// The euler rotation of the object.
        /// </summary>
        public Vector3 eulerRotation;

        // TODO: NetworkedVars?

        public static SpawnObjectPacket Create(NetworkIdentity identity) {
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
        
        public void Serialize(NetworkWriter writer) {
            writer.WriteBool(isPlayerObject);

            writer.WritePackedUInt64(networkID);
            writer.WritePackedUInt64(ownerClientID);
            writer.WritePackedUInt32(sceneID);

            writer.WriteBool(hasParent);
            if (hasParent)
                writer.WritePackedUInt64(parentNetID);

            writer.WriteBool(isSceneObject ?? true);
            if (isSceneObject ?? true)
                writer.WritePackedUInt64(networkedInstanceID);
            else writer.WritePackedUInt64(prefabHash);
            
            writer.WriteBool(includesTransform);
            if (includesTransform) {
                writer.WriteVector3(position);
                writer.WriteVector3(eulerRotation);
            }
        }

        public void DeSerialize(NetworkReader reader) {
            isPlayerObject = reader.ReadBool();

            networkID = reader.ReadPackedUInt64();
            ownerClientID = reader.ReadPackedUInt64();
            sceneID = reader.ReadPackedUInt32();

            hasParent = reader.ReadBool();
            if (hasParent)
                parentNetID = reader.ReadPackedUInt64();
            
            isSceneObject = reader.ReadBool();
            if (isSceneObject.Value)
                networkedInstanceID = reader.ReadPackedUInt64();
            else prefabHash = reader.ReadPackedUInt64();

            includesTransform = reader.ReadBool();
            if (includesTransform) {
                position = reader.ReadVector3();
                eulerRotation = reader.ReadVector3();
            }
        }

        public bool BufferPacket(NetworkChannel channel, ulong sender) {
            // BIG TODO: Also buffer if parent is not yet spawned!
            if (NetworkManager.instance.connectedClients[NetworkManager.instance.localClientID].loadedScenes
                              .Contains(sceneID))
                return false;
            NetworkSceneManager.bufferedScenePackets.Enqueue(sceneID, new BufferedPacket(this, sender, channel));
            return true;
        }
    }
}