using FNNLib.Messaging;
using FNNLib.Serialization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FNNLib.SceneManagement {
    [ClientPacket]
    internal class SceneChangePacket : IPacket {
        /// <summary>
        /// The index of the scene to load.
        /// This is the index of the scene in the allowed scenes list.
        /// </summary>
        public int sceneIndex;

        /// <summary>
        /// Client scene load mode.
        /// </summary>
        public LoadSceneMode mode;

        /// <summary>
        /// The scene network ID.
        /// This is used to distinguish between subscenes that are the same.
        /// </summary>
        public uint sceneNetID;

        /// <summary>
        /// The scene offset (for subscenes which will not be based in 0,0 by default).
        /// </summary>
        public Vector3 sceneOffset;
        
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedInt32(sceneIndex);
            writer.WriteByte((byte) mode);
            writer.WritePackedUInt32(sceneNetID);
            writer.WriteVector3(sceneOffset);
        }

        public void DeSerialize(NetworkReader reader) {
            sceneIndex = reader.ReadPackedInt32();
            mode = (LoadSceneMode) reader.ReadByte();
            sceneNetID = reader.ReadPackedUInt32();
            sceneOffset = reader.ReadVector3();
        }
    }
}