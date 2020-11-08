using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.SceneManagement {
    [ClientPacket]
    public class SceneUnloadPacket : ISerializable {
        public NetworkScene scene;
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt32(scene.ID);
        }

        public void DeSerialize(NetworkReader reader) {
            scene = NetworkSceneManager.GetScene(reader.ReadPackedUInt32());
        }
    }
}