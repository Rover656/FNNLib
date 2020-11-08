using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.Spawning {
    /// <summary>
    /// Change the owner of an object on clients.
    /// </summary>
    [ClientPacket]
    public class OwnerChangedPacket : ISerializable, IBufferablePacket {
        /// <summary>
        /// The network ID of the changed object.
        /// </summary>
        public ulong networkID;
        
        /// <summary>
        /// The new owner ID.
        /// </summary>
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
            if (SpawnManager.IsSpawned(networkID))
                return false;
            
            // Add to spawnmanager buffer so that this event is raised once the object exists
            SpawnManager.identityPacketBuffer.Enqueue(networkID, new BufferedPacket(this, sender, channel));
            return true;
        }
    }
}