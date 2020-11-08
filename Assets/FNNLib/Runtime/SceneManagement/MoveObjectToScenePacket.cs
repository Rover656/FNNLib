using FNNLib.Messaging;
using FNNLib.Serialization;
using FNNLib.Spawning;

namespace FNNLib.SceneManagement {
    [ClientPacket]
    public class MoveObjectToScenePacket : ISerializable, IBufferablePacket {
        public ulong networkID;
        public uint destinationScene;
        
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(networkID);
            writer.WritePackedUInt32(destinationScene);
        }

        public void DeSerialize(NetworkReader reader) {
            networkID = reader.ReadPackedUInt64();
            destinationScene = reader.ReadPackedUInt32();
        }

        public bool BufferPacket(NetworkChannel channel, ulong sender) {
            // Buffer in spawn manager
            if (!SpawnManager.IsSpawned(networkID)) {
                SpawnManager.identityPacketBuffer.Enqueue(networkID, new BufferedPacket(this, sender, channel));
                return true;
            }
            
            // Buffer in the scene manager
            if (NetworkSceneManager.IsSceneLoaded(destinationScene)) {
                NetworkSceneManager.bufferedScenePackets.Enqueue(destinationScene, new BufferedPacket(this, sender, channel));
                return true;
            }
            
            return false;
        }
    }
}