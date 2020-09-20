using System;
using System.Collections.Generic;
using FNNLib.Messaging;
using FNNLib.Serialization;
using FNNLib.Spawning;

namespace FNNLib.RPC {
    [ClientPacket, ServerPacket]
    public class RPCPacket : ISerializable, IBufferablePacket {
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

        public bool BufferPacket(ulong sender) {
            if (SpawnManager.spawnedObjects.ContainsKey(networkID))
                return false;
            
            // Add to spawnmanager buffer so that this event is raised once the object exists (or the 1 minute buffer time expires)
            SpawnManager.networkObjectPacketBuffer.Enqueue(networkID, new BufferedPacket(this, sender));
            return true;
        }
    }
}