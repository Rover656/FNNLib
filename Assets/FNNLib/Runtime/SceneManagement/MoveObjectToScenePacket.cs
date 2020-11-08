using FNNLib.Messaging;
using FNNLib.Serialization;
using FNNLib.Spawning;

namespace FNNLib.SceneManagement {
    [ClientPacket]
    public class MoveObjectToScenePacket : ISerializable, IBufferablePacket {
        public ulong networkID;
        public NetworkScene destinationScene;
        
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(networkID);
            writer.WritePackedUInt32(destinationScene.ID);
        }

        public void DeSerialize(NetworkReader reader) {
            networkID = reader.ReadPackedUInt64();
            destinationScene = NetworkSceneManager.GetScene(reader.ReadPackedUInt32());
        }

        public bool BufferPacket(NetworkChannel channel, ulong sender) {
            // Buffer in spawn manager
            if (!SpawnManager.spawnedIdentities.ContainsKey(networkID)) {
                SpawnManager.identityPacketBuffer.Enqueue(networkID, new BufferedPacket(this, sender, channel));
                return true;
            }
            
            // Buffer in the scene manager
            if (!NetworkManager.instance.connectedClients[NetworkManager.instance.localClientID].loadedScenes
                               .Contains(destinationScene)) {
                NetworkSceneManager.bufferedScenePackets.Enqueue(destinationScene.ID, new BufferedPacket(this, sender, channel));
                return true;
            }
            
            return false;
        }
    }
}