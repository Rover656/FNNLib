using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.Messaging {
    /// <summary>
    /// A buffered packet.
    /// </summary>
    public struct BufferedPacket {
        /// <summary>
        /// The packet itself.
        /// </summary>
        public ISerializable packet;
        
        /// <summary>
        /// The packet sender.
        /// </summary>
        public ulong sender;
        
        /// <summary>
        /// The time the packet was received.
        /// </summary>
        public float receiveTime;
        
        /// <summary>
        /// The channel the packet was received on, and should be handled by.
        /// </summary>
        public NetworkChannel channel;
        
        public BufferedPacket(ISerializable packet, ulong sender, NetworkChannel channel) {
            this.packet = packet;
            this.sender = sender;
            this.channel = channel;
            receiveTime = Time.unscaledTime; // TODO: networktime
        }
    }
}