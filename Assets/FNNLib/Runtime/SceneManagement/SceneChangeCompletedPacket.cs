using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.SceneManagement {
    [ServerPacket]
    public class SceneChangeCompletedPacket : IPacket {
        // TODO: Send the scene ID or something as validation...
        public void Serialize(NetworkWriter writer) {}
        public void DeSerialize(NetworkReader reader) {}
    }
}