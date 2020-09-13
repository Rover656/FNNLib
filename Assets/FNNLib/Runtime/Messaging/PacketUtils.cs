using System;
using FNNLib.Utilities;
using UnityEngine;

namespace FNNLib.Messaging {
    public static class PacketUtils {
        /// <summary>
        /// Gets a packet ID from the type's name hash.
        /// </summary>
        /// <typeparam name="T">The packet type.</typeparam>
        /// <returns>The packet's ID.</returns>
        [Obsolete("Use GetID32 instead.")]
        public static uint GetID<T>() where T : IPacket {
            return typeof(T).FullName.GetStableHash32() & 0xFFFF;
        }

        public static ushort GetID16<T>() where T : IPacket {
            return typeof(T).FullName.GetStableHash16();
        }
        
        public static uint GetID32<T>() where T : IPacket {
            return typeof(T).FullName.GetStableHash32();
        }
        
        public static ulong GetID64<T>() where T : IPacket {
            return typeof(T).FullName.GetStableHash64();
        }
        
        /// <summary>
        /// Determine if this packet is run on the client.
        /// </summary>
        /// <typeparam name="T">The packet type.</typeparam>
        /// <returns>Whether the packet can be sent to the client.</returns>
        public static bool IsClientPacket<T>() {
            return typeof(T).GetCustomAttributes(typeof(ClientPacketAttribute), false).Length > 0;
        }
        
        /// <summary>
        /// Determine if this packet is run on the server.
        /// </summary>
        /// <typeparam name="T">The packet type.</typeparam>
        /// <returns>Whether the packet can be sent to the server.</returns>
        public static bool IsServerPacket<T>() {
            return typeof(T).GetCustomAttributes(typeof(ServerPacketAttribute), false).Length > 0;
        }
        
        /// <summary>
        /// Gets a lambda function that will be used by the PacketHandler.
        /// </summary>
        /// <param name="handler">The handler to be called when the packet is recieved.</param>
        /// <typeparam name="T">The packet the handler is for.</typeparam>
        /// <returns>The handler for the PacketHandler internal system.</returns>
        internal static NetworkPacketDelegate GetPacketHandler<T>(Action<ulong, T> handler)
            where T : IPacket, new()
            => (clientID, reader) => {
                   // Handle the incoming packet
                   T message = default;
                   try {
                       // Create the message
                       message = default(T) != null ? default(T) : new T();
                       message.DeSerialize(reader);
                   }
                   catch (Exception ex) {
                       Debug.LogError("Packet handler exception occurred! " + ex);
                   }

                   // Send to the handler
                   handler(clientID, message);
               };
    }
}