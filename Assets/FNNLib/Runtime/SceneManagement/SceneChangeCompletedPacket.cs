using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.SceneManagement {
    [ServerPacket]
    internal class SceneChangeCompletedPacket : IPacket {
        public uint loadedSceneID;

        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt32(loadedSceneID);
        }

        public void DeSerialize(NetworkReader reader) {
            loadedSceneID = reader.ReadPackedUInt32();
        }
    }
}