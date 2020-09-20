using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.RPC {
    /// <summary>
    /// An RPC response.
    /// </summary>
    [ClientPacket, ServerPacket]
    public class RPCResponsePacket : ISerializable {
        public ulong responseID;
        public object result;

        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(responseID);
            writer.WritePackedObject(result);
        }

        public void DeSerialize(NetworkReader reader) {
            responseID = reader.ReadPackedUInt64();
            if (RPCResponseManager.Contains(responseID))
                result = reader.ReadPackedObject(RPCResponseManager.Get(responseID).resultType);
        }
    }
}