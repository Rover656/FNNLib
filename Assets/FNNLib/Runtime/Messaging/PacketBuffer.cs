using System;
using System.Collections.Generic;
using UnityEngine;

namespace FNNLib.Messaging {
    /// <summary>
    /// A packet buffer.
    /// This stores packets that are queued for later execution.
    /// </summary>
    public class PacketBuffer {
        /// <summary>
        /// The queue containing all buffered packets.
        /// </summary>
        private Queue<BufferedPacket> _internalQueue = new Queue<BufferedPacket>();

        /// <summary>
        /// Number of packets in the queue.
        /// </summary>
        public int count => _internalQueue.Count;

        /// <summary>
        /// Enqueue a buffered packet.
        /// </summary>
        /// <param name="packet">The buffered packet.</param>
        public void Enqueue(BufferedPacket packet) {
            _internalQueue.Enqueue(packet);
        }

        /// <summary>
        /// Execute the oldest packet in the buffer.
        /// </summary>
        public void ExecutePending() {
            // Do nothing if the queue is empty
            if (_internalQueue.Count == 0)
                return;
            
            // Get the back packet.
            var packet = _internalQueue.Dequeue();
            if (IsPacketOld(packet))
                return;
            
            // If this packet still has a buffer problem. See if it needs buffered again.
            // This can happen for example if something is moving scene but the scene doesn't exist *and* the object doesnt exist.
            if (packet.packet is IBufferablePacket bufferablePacket) {
                if (bufferablePacket.BufferPacket(packet.channel, packet.sender))
                    return;
            }

            // Process the buffers.
            if (NetworkManager.instance.isClient)
                packet.channel.HandleBuffered(packet, false);
            if (NetworkManager.instance.isServer)
                packet.channel.HandleBuffered(packet, true);
        }

        /// <summary>
        /// Purge all old packets.
        /// </summary>
        public void PurgeOldPackets() {
            // Remove old packets from the buffer
            var back = _internalQueue.Peek();
            while (IsPacketOld(back)) {
                _internalQueue.Dequeue();
                back = _internalQueue.Peek();
            }
        }

        /// <summary>
        /// Determine if a packet is old.
        /// </summary>
        /// <param name="packet">The packet to check</param>
        /// <returns>Whether the packet is too old.</returns>
        private bool IsPacketOld(BufferedPacket packet) {
            return Time.unscaledTime - packet.receiveTime >= NetworkManager.instance.networkConfig.maxBufferedPacketAge;
        }
    }
}