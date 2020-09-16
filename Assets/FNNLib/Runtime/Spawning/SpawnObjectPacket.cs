using FNNLib.Messaging;
using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.Spawning {
    [ClientPacket]
    internal class SpawnObjectPacket : IPacket {
        public bool isPlayerObject;

        public ulong networkID;
        public ulong ownerClientID;
        public ulong sceneID;

        public bool hasParent;
        public ulong parentNetID;

        public bool? isSceneObject;
        public ulong networkedInstanceID;
        public ulong prefabHash;

        public bool includesTransform;
        public Vector3 position;
        public Vector3 eulerRotation;

        // TODO: NetworkedVars?

        public void Serialize(NetworkWriter writer) {
            writer.WriteBool(isPlayerObject);

            writer.WritePackedUInt64(networkID);
            writer.WritePackedUInt64(ownerClientID);
            writer.WritePackedUInt64(sceneID);

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
            sceneID = reader.ReadPackedUInt64();

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
    }
}