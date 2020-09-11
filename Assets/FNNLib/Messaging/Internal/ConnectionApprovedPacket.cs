using FNNLib.Serialization;

namespace FNNLib.Messaging.Internal {
    [ClientPacket]
    public class ConnectionApprovedPacket : IPacket {
        public int localClientID;

        public void Serialize(NetworkWriter writer) {
            writer.WriteInt32(localClientID);
        }

        public void DeSerialize(NetworkReader reader) {
            localClientID = reader.ReadInt32();
        }
    }
}