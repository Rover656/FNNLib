using System;
using System.Collections.Generic;
using FNNLib.Config;
using FNNLib.Messaging;
using FNNLib.Serialization;
using FNNLib.Spawning;
using UnityEngine;

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
            
            writer.WriteSegmentWithSize(parameterBuffer);
        }

        public void DeSerialize(NetworkReader reader) {
            networkID = reader.ReadPackedUInt64();
            behaviourOrder = reader.ReadPackedInt32();

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
            
            var paramBuf = reader.ReadSegmentWithSize();
            if (paramBuf == null)
                throw new NullReferenceException("Parameter buffer was null!");
            parameterBuffer = paramBuf.Value;
        }

        public bool BufferPacket(ulong sender, int channel) {
            if (SpawnManager.spawnedObjects.ContainsKey(networkID))
                return false;
            
            // Add to spawnmanager buffer so that this event is raised once the object exists (or the 1 minute buffer time expires)
            SpawnManager.networkObjectPacketBuffer.Enqueue(networkID, new BufferedPacket(this, sender, channel));
            return true;
        }
    }
}