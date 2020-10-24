using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.Spawning {
    [ClientPacket]
    internal class DestroyObjectPacket : ISerializable, IBufferablePacket {
        public ulong networkID;
        
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(networkID);
        }

        public void DeSerialize(NetworkReader reader) {
            networkID = reader.ReadPackedUInt64();
        }
        
        public bool BufferPacket(NetworkChannel channel, ulong sender) {
            if (SpawnManager.spawnedObjects.ContainsKey(networkID))
                return false;
            
            // Add to spawnmanager buffer so that this event is raised once the object exists (or the 1 minute buffer time expires)
            SpawnManager.networkObjectPacketBuffer.Enqueue(networkID, new BufferedPacket(this, sender, channel));
            return true;
        }
    }
}