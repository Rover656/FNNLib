using FNNLib.Serialization;

namespace FNNLib.Messaging.Internal {
    [ServerPacket]
    public class ConnectionRequestPacket : IPacket {
        public int protocolVersion;
        public byte[] connectionData;
        
        public void Serialize(NetworkWriter writer) {
            writer.WriteInt32(protocolVersion);
            writer.WriteBytesWithSize(connectionData, 0, connectionData?.Length ?? 0);
        }

        public void DeSerialize(NetworkReader reader) {
            protocolVersion = reader.ReadInt32();
            connectionData = reader.ReadBytesWithSize();
        }
    }
}