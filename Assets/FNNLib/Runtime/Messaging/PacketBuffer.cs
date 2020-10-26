using System;
using System.Collections.Generic;
using UnityEngine;

namespace FNNLib.Messaging {
    public class PacketBuffer {
        private Queue<BufferedPacket> _internalQueue = new Queue<BufferedPacket>();

        public int count => _internalQueue.Count;

        public void Enqueue(BufferedPacket packet) {
            if (_internalQueue.Contains(packet))
                throw new Exception();
            _internalQueue.Enqueue(packet);
        }

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
            // TODO: Need to add another age value, adding a limit to the times a packet can be rebuffered.
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

        public void PurgeOldPackets() {
            // Remove old packets from the buffer
            var back = _internalQueue.Peek();
            while (IsPacketOld(back)) {
                _internalQueue.Dequeue();
                back = _internalQueue.Peek();
            }
        }

        private bool IsPacketOld(BufferedPacket packet) {
            return Time.unscaledTime - packet.receiveTime >= NetworkManager.instance.networkConfig.maxBufferedPacketAge;
        }
    }
}