using System;
using System.Collections.Generic;
using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.Messaging {
    public delegate void NetworkPacketDelegate(ulong clientID, NetworkReader reader);
    
    public abstract class PacketHandler {
        /// <summary>
        /// If this is a server context, packets must have the ServerPacket attribute.
        /// Otherwise it must have ClientPacket.
        /// A packet can have both.
        /// </summary>
        protected abstract bool isServerContext { get; }
        
        /// <summary>
        /// This contains a list of packet handlers for each packet ID.
        /// </summary>
        private readonly Dictionary<int, NetworkPacketDelegate> _packetHandlers = new Dictionary<int, NetworkPacketDelegate>();

        /// <summary>
        /// Handle an incoming packet.
        /// </summary>
        /// <param name="sender">The senders ID (0 for server).</param>
        /// <param name="data">The data containing the packet.</param>
        protected void HandlePacket(ulong sender, ArraySegment<byte> data, int channelID) {
            // TODO: A non-allocating reader/writer like Mirror.
            using (var reader = NetworkReaderPool.GetReader(data)) {
                var packetID = reader.ReadInt32();
                if (_packetHandlers.TryGetValue(packetID, out var handler)) {
                    handler(sender, reader);
                } else {
                    Debug.LogWarning("Received an unknown or unsupported packet from client " + sender + ". Ignoring.");
                }
            }
        }

        /// <summary>
        /// Register a packet handler.
        /// </summary>
        /// <param name="handler">The handler to control this packet</param>
        /// <typeparam name="T">The packet type to be handled.</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if a packet name/hash conflict occurs.</exception>
        public void RegisterPacketHandler<T>(Action<ulong, T> handler) where T : IPacket, new() {
            if (isServerContext && !PacketUtils.IsServerPacket<T>())
                throw new InvalidOperationException("To register a packet on the server, it must have the ServerPacket attribute.");
            if (!isServerContext && !PacketUtils.IsClientPacket<T>())
                throw new InvalidOperationException("To register a packet on the client, it must have the ClientPacket attribute.");
            
            var packetID = PacketUtils.GetID<T>();
            if (_packetHandlers.ContainsKey(packetID))
                throw new InvalidOperationException("A packet with this ID already exists, ensure that you have given it a unique name!");
            _packetHandlers.Add(packetID, PacketUtils.GetPacketHandler(handler));
        }

        /// <summary>
        /// Clear the packet handler for a given packet type.
        /// </summary>
        /// <typeparam name="T">The packet type to clear.</typeparam>
        /// <exception cref="InvalidOperationException">Thrown if a handler does not exist for this packet.</exception>
        public void ClearPacketHandler<T>() where T : IPacket, new() {
            var id = PacketUtils.GetID<T>();
            if (!_packetHandlers.ContainsKey(id))
                throw new InvalidOperationException("No handler exists for this packet!");
            _packetHandlers.Remove(id);
        }

        /// <summary>
        /// Clear the packet handlers.
        /// Used if you are resetting the handler.
        /// </summary>
        internal void ClearPacketHandlers() {
            _packetHandlers.Clear();
        }
    }
}