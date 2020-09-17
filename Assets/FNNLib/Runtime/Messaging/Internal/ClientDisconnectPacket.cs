using FNNLib.Serialization;

namespace FNNLib.Messaging.Internal {
    [ClientPacket]
    internal class ClientDisconnectPacket : ISerializable {
        /// <summary>
        /// The reason that the client was disconnected.
        /// </summary>
        public string disconnectReason;
        
        public void Serialize(NetworkWriter writer) {
            writer.WriteString(disconnectReason);
        }

        public void DeSerialize(NetworkReader reader) {
            disconnectReason = reader.ReadString();
        }
    }
}