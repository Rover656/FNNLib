using System;
using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.Messaging {
    public delegate void ServerPacketDelegate(ulong clientID, NetworkReader reader, int channel);

    public delegate void ClientPacketDelegate(NetworkReader reader, int channel);
    
    internal static class PacketHandlers {
        public static ServerPacketHandlers GetServerHandlers<T>(Action<ulong, T, int> action) where T : ISerializable, new() {
            return new ServerPacketHandlers {
                                                packetAction = (clientID, basePacket, channel) =>
                                                                   action(clientID, (T) basePacket, channel),
                                                packetDelegate = GetServerPacketDelegate(action)
                                            };
        }
        
        public static ClientPacketHandlers GetClientHandlers<T>(Action<T, int> action) where T : ISerializable, new() {
            return new ClientPacketHandlers {
                                                packetAction = (basePacket, channel) =>
                                                                   action((T) basePacket, channel),
                                                packetDelegate = GetClientPacketDelegate(action)
                                            };
        }

        private static ServerPacketDelegate GetServerPacketDelegate<T>(Action<ulong, T, int> action)
            where T : ISerializable, new() => (clientID, reader, channel) => {
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
                                                      if (bufferablePacket.BufferPacket(clientID, channel))
                                                          return;
                                                  }

                                                  // Call the handler
                                                  action(clientID, packet, channel);
                                              };
        
        private static ClientPacketDelegate GetClientPacketDelegate<T>(Action<T, int> action)
            where T : ISerializable, new() => (reader, channel) => {
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
                                                      if (bufferablePacket.BufferPacket(NetworkManager.ServerLocalID, channel))
                                                          return;
                                                  }

                                                  // Call the handler
                                                  action(packet, channel);
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
        public Action<ulong, ISerializable, int> packetAction;
    }
    
    internal struct ClientPacketHandlers {
        /// <summary>
        /// The packet delegate, called when receiving data with a reader.
        /// </summary>
        public ClientPacketDelegate packetDelegate;
        
        /// <summary>
        /// The packet action, called when using buffered packets.
        /// </summary>
        public Action<ISerializable, int> packetAction;
    }
}