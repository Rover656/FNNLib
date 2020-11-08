using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.SceneManagement {
    [ClientPacket]
    public class SceneUnloadPacket : ISerializable {
        public uint sceneID;
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt32(sceneID);
        }

        public void DeSerialize(NetworkReader reader) {
            sceneID = reader.ReadPackedUInt32();
        }
        
        // TODO: Should probably buffer this...
    }
}