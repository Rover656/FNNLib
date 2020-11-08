using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.Spawning {
    /// <summary>
    /// Destroy an object on the client.
    /// </summary>
    [ClientPacket]
    internal class DestroyObjectPacket : ISerializable, IBufferablePacket {
        /// <summary>
        /// Network ID of the object to destroy.
        /// </summary>
        public ulong networkID;
        
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(networkID);
        }

        public void DeSerialize(NetworkReader reader) {
            networkID = reader.ReadPackedUInt64();
        }
        
        public bool BufferPacket(NetworkChannel channel, ulong sender) {
            if (SpawnManager.IsSpawned(networkID))
                return false;
            
            // Add to spawnmanager buffer so that this event is raised once the object exists (or the 1 minute buffer time expires)
            SpawnManager.identityPacketBuffer.Enqueue(networkID, new BufferedPacket(this, sender, channel));
            return true;
        }
    }
}