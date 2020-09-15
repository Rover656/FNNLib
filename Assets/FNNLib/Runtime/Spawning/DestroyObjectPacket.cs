using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.Spawning {
    [ClientPacket]
    internal class DestroyObjectPacket : IPacket {
        public ulong networkID;
        
        // TODO: Do we need to validate scene ID?
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(networkID);
        }

        public void DeSerialize(NetworkReader reader) {
            networkID = reader.ReadPackedUInt64();
        }
    }
}