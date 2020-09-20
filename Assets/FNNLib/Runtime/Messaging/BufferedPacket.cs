using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.Messaging {
    public struct BufferedPacket {
        public ISerializable packet;
        public ulong sender;
        public float receiveTime;
        public int channel;

        public BufferedPacket(ISerializable packet, ulong sender, int channel) {
            this.packet = packet;
            this.sender = sender;
            this.channel = channel;
            receiveTime = Time.unscaledTime; // TODO: networktime
        }
    }
}