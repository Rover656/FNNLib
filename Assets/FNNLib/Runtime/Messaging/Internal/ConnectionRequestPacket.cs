using FNNLib.Serialization;

namespace FNNLib.Messaging.Internal {
    [ServerPacket]
    internal class ConnectionRequestPacket : ISerializable {
        public ulong verificationHash;
        public ISerializable connectionData;
        
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(verificationHash);
            writer.WritePackedObject(connectionData);
        }

        public void DeSerialize(NetworkReader reader) {
            verificationHash = reader.ReadPackedUInt64();
            connectionData = (ISerializable) reader.ReadPackedObject(typeof(ISerializable));
        }
    }
}