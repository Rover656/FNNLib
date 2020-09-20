using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.Messaging {
    public struct BufferedPacket {
        public ISerializable packet;
        public ulong sender;
        public float receiveTime;
        // public int channel;

        public BufferedPacket(ISerializable packet, ulong sender) {
            this.packet = packet;
            this.sender = sender;
            receiveTime = Time.unscaledTime; // TODO: networktime
        }
    }
}