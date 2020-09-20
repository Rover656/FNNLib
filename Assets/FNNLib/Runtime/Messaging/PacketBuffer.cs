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
            
            // Get packet ID
            var packetID = NetworkManager.instance.GetPacketID(packet.packet.GetType());
            
            // Invoke handlers.
            if (NetworkManager.instance.isClient && NetworkManager.instance.clientHandlers.ContainsKey(packetID))
                NetworkManager.instance.clientHandlers[packetID].packetAction(packet.packet);
            if (NetworkManager.instance.isServer && NetworkManager.instance.serverHandlers.ContainsKey(packetID))
                NetworkManager.instance.serverHandlers[packetID].packetAction(packet.sender, packet.packet);
        }

        public void PurgeOldPackets() {
            foreach (var bufferedPacket in _internalList) {
                if (IsPacketOld(bufferedPacket))
                    _internalList.Remove(bufferedPacket);
            }
        }

        private bool IsPacketOld(BufferedPacket packet) {
            return Time.unscaledTime - packet.receiveTime >= NetworkManager.instance.networkConfig.maxBufferedPacketAge;
        }
    }
}