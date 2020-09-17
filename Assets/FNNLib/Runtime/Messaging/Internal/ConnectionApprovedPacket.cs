using FNNLib.Serialization;

namespace FNNLib.Messaging.Internal {
    [ClientPacket]
    internal class ConnectionApprovedPacket : ISerializable {
        public ulong localClientID;

        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(localClientID);
        }

        public void DeSerialize(NetworkReader reader) {
            localClientID = reader.ReadPackedUInt64();
        }
    }
}