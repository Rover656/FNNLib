using System;
using FNNLib.Serialization;
using FNNLib.Utilities;
using UnityEngine;

namespace FNNLib.Messaging {
    public static class PacketUtils {
        public static ushort GetID16<T>() where T : ISerializable {
            return typeof(T).FullName.GetStableHash16();
        }
        
        public static uint GetID32<T>() where T : ISerializable {
            return typeof(T).FullName.GetStableHash32();
        }
        
        public static ulong GetID64<T>() where T : ISerializable {
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
        
        internal static ServerPacketDelegate GetServerPacketDelegate<T>(Action<ulong, T> handler)
            where T : ISerializable, new()
            => (clientID, reader) => {
                   // Handle the incoming packet
                   T message = default;
                   try {
                       // Create the message
                       message = (T) reader.ReadPackedObject(typeof(T));
                   }
                   catch (Exception ex) {
                       Debug.LogError("Packet handler exception occurred! " + ex);
                   }

                   // Send to the handler
                   handler(clientID, message);
               };
        
        internal static ClientPacketDelegate GetClientPacketDelegate<T>(Action<T> handler)
            where T : ISerializable, new()
            => (reader) => {
                   // Handle the incoming packet
                   T message = default;
                   try {
                       // Create the message
                       message = (T) reader.ReadPackedObject(typeof(T));
                   }
                   catch (Exception ex) {
                       Debug.LogError("Packet handler exception occurred! " + ex);
                   }

                   // Send to the handler
                   handler(message);
               };
    }
}