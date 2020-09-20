using FNNLib.Messaging;
using FNNLib.Serialization;
using UnityEngine.SceneManagement;

namespace FNNLib.SceneManagement {
    [ClientPacket]
    public class SceneLoadPacket : ISerializable {
        public uint sceneNetworkID;
        public int sceneIndex;
        public LoadSceneMode mode;

        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt32(sceneNetworkID);
            writer.WritePackedInt32(sceneIndex);
            writer.WriteByte((byte) mode);
        }

        public void DeSerialize(NetworkReader reader) {
            sceneNetworkID = reader.ReadPackedUInt32();
            sceneIndex = reader.ReadPackedInt32();
            mode = (LoadSceneMode) reader.ReadByte();
        }
    }
}