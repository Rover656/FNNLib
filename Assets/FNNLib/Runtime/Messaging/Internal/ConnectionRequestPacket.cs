using FNNLib.Serialization;

namespace FNNLib.Messaging.Internal {
    [ServerPacket]
    internal class ConnectionRequestPacket : ISerializable {
        public ulong verificationHash;
        public byte[] connectionData;
        
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(verificationHash);
            writer.WriteBytesWithSize(connectionData, 0, connectionData?.Length ?? 0);
        }

        public void DeSerialize(NetworkReader reader) {
            verificationHash = reader.ReadPackedUInt64();
            connectionData = reader.ReadBytesWithSize();
        }
    }
}