using System.IO;
using FNNLib.Serialization;

namespace FNNLib.Messaging.Internal {
    [ServerPacket]
    public class ConnectionRequestPacket : IPacket {
        public byte[] connectionData = new byte[0];
        
        public void Serialize(NetworkWriter writer) {
            writer.WriteBytesWithSize(connectionData, 0, connectionData.Length);
        }

        public void DeSerialize(NetworkReader reader) {
            connectionData = reader.ReadBytesWithSize();
        }
    }
}