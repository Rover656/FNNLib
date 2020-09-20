using System;
using FNNLib.Serialization;
using FNNLib.Utilities;
using UnityEngine;

namespace FNNLib.Messaging {
    public static class PacketUtils {
        public static ushort GetID16<T>() where T : ISerializable {
            return typeof(T).FullName.GetStableHash16();
        }
        
        public static ushort GetID16(Type type) {
            if (!typeof(ISerializable).IsAssignableFrom(type))
                throw new InvalidOperationException("The type is not derived from ISerializable!");
            return type.FullName.GetStableHash16();
        }

        public static uint GetID32<T>() where T : ISerializable {
            return typeof(T).FullName.GetStableHash32();
        }
        
        public static uint GetID32(Type type) {
            if (!typeof(ISerializable).IsAssignableFrom(type))
                throw new InvalidOperationException("The type is not derived from ISerializable!");
            return type.FullName.GetStableHash32();
        }

        public static ulong GetID64<T>() where T : ISerializable {
            return typeof(T).FullName.GetStableHash64();
        }
        
        public static ulong GetID64(Type type) {
            if (!typeof(ISerializable).IsAssignableFrom(type))
                throw new InvalidOperationException("The type is not derived from ISerializable!");
            return type.FullName.GetStableHash64();
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
    }
}