using System;
using FNNLib.Config;
using FNNLib.Messaging;
using FNNLib.Serialization;
using FNNLib.Spawning;

namespace FNNLib.RPC {
    [ClientPacket, ServerPacket]
    public class RPCPacket : ISerializable, IBufferablePacket {
        public ulong networkID;
        public int behaviourIndex;
        public ulong methodHash;
        public bool expectsResponse;
        public ulong responseID;
        public ArraySegment<byte> parameterBuffer;

        public void Serialize(NetworkWriter writer) {
            writer.WritePackedUInt64(networkID);
            writer.WritePackedInt32(behaviourIndex);

            switch (NetworkManager.instance.networkConfig.rpcHashSize) {
                case HashSize.TwoBytes:
                    writer.WritePackedUInt16((ushort) methodHash);
                    break;
                case HashSize.FourBytes:
                    writer.WritePackedUInt32((uint) methodHash);
                    break;
                case HashSize.EightBytes:
                    writer.WritePackedUInt64(methodHash);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            writer.WriteBool(expectsResponse);
            if (expectsResponse)
                writer.WritePackedUInt64(responseID);
            
            writer.WriteSegmentWithSize(parameterBuffer);
        }

        public void DeSerialize(NetworkReader reader) {
            networkID = reader.ReadPackedUInt64();
            behaviourIndex = reader.ReadPackedInt32();

            switch (NetworkManager.instance.networkConfig.rpcHashSize) {
                case HashSize.TwoBytes:
                    methodHash = reader.ReadPackedUInt16();
                    break;
                case HashSize.FourBytes:
                    methodHash = reader.ReadPackedUInt32();
                    break;
                case HashSize.EightBytes:
                    methodHash = reader.ReadPackedUInt64();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            expectsResponse = reader.ReadBool();
            if (expectsResponse)
                responseID = reader.ReadPackedUInt64();
            
            var paramBuf = reader.ReadSegmentWithSize();
            if (paramBuf == null)
                throw new NullReferenceException("Parameter buffer was null!");
            parameterBuffer = paramBuf.Value;
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