using System.IO;
using FNNLib.Messaging;
using FNNLib.Serialization;

namespace DefaultNamespace {
    [ClientPacket, ServerPacket]
    public class TestPacket : IPacket {
        public string text;
        
        public void Serialize(NetworkWriter writer) {
            writer.WriteString(text);
        }

        public void DeSerialize(NetworkReader reader) {
            text = reader.ReadString();
        }
    }
}