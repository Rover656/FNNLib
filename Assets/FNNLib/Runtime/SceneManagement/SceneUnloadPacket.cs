using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.SceneManagement {
    [ClientPacket]
    public class SceneUnloadPacket : ISerializable {
        public uint sceneNetID;
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt32(sceneNetID);
        }

        public void DeSerialize(NetworkReader reader) {
            sceneNetID = reader.ReadPackedUInt32();
        }
    }
}