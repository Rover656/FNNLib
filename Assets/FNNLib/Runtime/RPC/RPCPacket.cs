using System;
using FNNLib.Messaging;
using FNNLib.Serialization;

namespace FNNLib.RPC {
    [ClientPacket, ServerPacket]
    public class RPCPacket : ISerializable {
        public ulong networkID;
        public int behaviourOrder;
        public ulong methodHash;
        public ArraySegment<byte> parameterBuffer;

        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(networkID);
            writer.WritePackedInt32(behaviourOrder);
            writer.WritePackedUInt64(methodHash);
            writer.WriteSegmentWithSize(parameterBuffer);
        }

        public void DeSerialize(NetworkReader reader) {
            networkID = reader.ReadPackedUInt64();
            behaviourOrder = reader.ReadPackedInt32();
            methodHash = reader.ReadPackedUInt64();
            var paramBuf = reader.ReadSegmentWithSize();
            if (paramBuf == null)
                throw new NullReferenceException("Parameter buffer was null!");
            parameterBuffer = paramBuf.Value;
        }
    }
}