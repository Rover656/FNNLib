using FNNLib.Serialization;

namespace FNNLib.Messaging.Internal {
    [ServerPacket]
    public class ConnectionRequestPacket : IPacket {
        public ulong protocolVersion;
        public byte[] connectionData;
        
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(protocolVersion);
            writer.WriteBytesWithSize(connectionData, 0, connectionData?.Length ?? 0);
        }

        public void DeSerialize(NetworkReader reader) {
            protocolVersion = reader.ReadPackedUInt64();
            connectionData = reader.ReadBytesWithSize();
        }
    }
}