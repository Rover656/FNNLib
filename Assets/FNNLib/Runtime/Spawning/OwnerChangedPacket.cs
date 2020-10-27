using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.Spawning {
    [ClientPacket]
    public class OwnerChangedPacket : ISerializable, IBufferablePacket {
        public ulong networkID;
        public ulong newOwnerID;
        
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(networkID);
            writer.WritePackedUInt64(newOwnerID);
        }

        public void DeSerialize(NetworkReader reader) {
            networkID = reader.ReadPackedUInt64();
            newOwnerID = reader.ReadPackedUInt64();
        }

        public bool BufferPacket(NetworkChannel channel, ulong sender) {
            if (SpawnManager.spawnedObjects.ContainsKey(networkID))
                return false;
            
            // Add to spawnmanager buffer so that this event is raised once the object exists
            SpawnManager.networkObjectPacketBuffer.Enqueue(networkID, new BufferedPacket(this, sender, channel));
            return true;
        }
    }
}