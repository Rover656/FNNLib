using FNNLib.Serialization;

namespace FNNLib.Messaging.Internal {
    [ClientPacket]
    public class ConnectionApprovedPacket : IPacket {
        public ulong localClientID;

        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(localClientID);
        }

        public void DeSerialize(NetworkReader reader) {
            localClientID = reader.ReadPackedUInt64();
        }
    }
}