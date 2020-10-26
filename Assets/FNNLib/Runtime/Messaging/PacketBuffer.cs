using System;
using System.Collections.Generic;
using UnityEngine;

namespace FNNLib.Messaging {
    public class PacketBuffer {
        private List<BufferedPacket> _internalList = new List<BufferedPacket>();

        public int count => _internalList.Count;

        public void Enqueue(BufferedPacket packet) {
            if (_internalList.Contains(packet))
                throw new Exception();
            _internalList.Add(packet);
        }

        public void ExecutePending() {
            if (_internalList.Count == 0)
                throw new InvalidOperationException("Queue is empty.");
            var packet = _internalList[0];
            _internalList.RemoveAt(0);
            if (IsPacketOld(packet))
                return;
            
            // If this packet still has a buffer problem. See if it needs buffered again.
            // This can happen for example if something is moving scene but the scene doesn't exist *and* the object doesnt exist.
            if (packet.packet is IBufferablePacket bufferablePacket) {
                if (bufferablePacket.BufferPacket(packet.channel, packet.sender))
                    return;
            }

            // Invoke handlers.
            if (NetworkManager.instance.isClient)
                packet.channel.HandleBuffered(packet, false);
            if (NetworkManager.instance.isServer)
                packet.channel.HandleBuffered(packet, true);
        }

        public void PurgeOldPackets() {
            // TODO: The list should be sorted so that the order is correct!
            for (var i = _internalList.Count - 1; i >= 0 && IsPacketOld(_internalList[i]); i--) {
                _internalList.Remove(_internalList[i]);
            }
        }

        private bool IsPacketOld(BufferedPacket packet) {
            return Time.unscaledTime - packet.receiveTime >= NetworkManager.instance.networkConfig.maxBufferedPacketAge;
        }
    }
}