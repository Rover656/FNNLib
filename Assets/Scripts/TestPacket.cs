using System.IO;
using FNNLib.Messaging;
using FNNLib.Serialization;

namespace DefaultNamespace {
    [ClientPacket, ServerPacket]
    public class TestPacket : AutoSerializer {
        public string text;
    }
}