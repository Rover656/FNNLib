using System;
using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.Messaging {
    internal static class PacketHandlers {
        public static ServerPacketHandlers GetServerHandlers<T>(Action<ulong, T> action) where T : ISerializable, new() {
            return new ServerPacketHandlers {
                                                packetAction = (clientID, basePacket) =>
                                                                   action(clientID, (T) basePacket),
                                                packetDelegate = GetServerPacketDelegate(action)
                                            };
        }
        
        public static ClientPacketHandlers GetClientHandlers<T>(Action<T> action) where T : ISerializable, new() {
            return new ClientPacketHandlers {
                                                packetAction = (basePacket) =>
                                                                   action((T) basePacket),
                                                packetDelegate = GetClientPacketDelegate(action)
                                            };
        }

        private static ServerPacketDelegate GetServerPacketDelegate<T>(Action<ulong, T> action)
            where T : ISerializable, new() => (clientID, reader) => {
                                                  // This will read the packet from the reader
                                                  T packet;
                                                  try {
                                                      // Try to read the message
                                                      packet = (T) reader.ReadPackedObject(typeof(T));
                                                  } catch (Exception ex) {
                                                      Debug.LogError("Exception ocurred when reading packet from reader: " + ex);
                                                      return;
                                                  }

                                                  // If the packet can be buffered, see if it must be buffered
                                                  if (packet is IBufferablePacket bufferablePacket) {
                                                      if (bufferablePacket.BufferPacket(clientID))
                                                          return;
                                                  }

                                                  // Call the handler
                                                  action(clientID, packet);
                                              };
        
        private static ClientPacketDelegate GetClientPacketDelegate<T>(Action<T> action)
            where T : ISerializable, new() => (reader) => {
                                                  // This will read the packet from the reader
                                                  T packet;
                                                  try {
                                                      // Try to read the message
                                                      packet = (T) reader.ReadPackedObject(typeof(T));
                                                  } catch (Exception ex) {
                                                      Debug.LogError("Exception ocurred when reading packet from reader: " + ex);
                                                      return;
                                                  }

                                                  // If the packet can be buffered, see if it must be buffered
                                                  if (packet is IBufferablePacket bufferablePacket) {
                                                      if (bufferablePacket.BufferPacket(NetworkManager.ServerLocalID))
                                                          return;
                                                  }

                                                  // Call the handler
                                                  action(packet);
                                              };
    }
    
    internal struct ServerPacketHandlers {
        /// <summary>
        /// The packet delegate, called when receiving data with a reader.
        /// </summary>
        public ServerPacketDelegate packetDelegate;
        
        /// <summary>
        /// The packet action, called when using buffered packets.
        /// </summary>
        public Action<ulong, ISerializable> packetAction;
    }
    
    internal struct ClientPacketHandlers {
        /// <summary>
        /// The packet delegate, called when receiving data with a reader.
        /// </summary>
        public ClientPacketDelegate packetDelegate;
        
        /// <summary>
        /// The packet action, called when using buffered packets.
        /// </summary>
        public Action<ISerializable> packetAction;
    }
}