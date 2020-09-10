using System.IO;
using FNNLib.Serialization;

namespace FNNLib.Messaging.Internal {
    // Delegate used for the event in NetworkClient.
    public delegate void ConnectionApprovedDelegate(int localClientID);
    
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