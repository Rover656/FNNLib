using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FNNLib.Config;
using FNNLib.Serialization;
using FNNLib.Transports;
using UnityEngine;

namespace FNNLib.Messaging {
    /// <summary>
    /// Represents a channel in the network which has a registry of packets it recognises.
    /// </summary>
    [Serializable]
    public sealed class NetworkChannel { 
        /// <summary>
        /// Message factory.
        /// Used for building message types and associating handlers.
        /// </summary>
        public class MessageFactory {
            /// <summary>
            /// The state of the factory.
            /// </summary>
            private enum FactoryState {
                Invalid,
                TypedConsumer,
                SidedTypedConsumer,
                ReaderConsumer,
                SidedReaderConsumer
            }
            
            public delegate void TypedConsumerMethod<in T>(NetworkChannel channel, T message, ulong sender, bool isServer) where T : ISerializable;
            public delegate void TypedServerConsumerMethod<in T>(NetworkChannel channel, T message, ulong sender) where T : ISerializable;
            public delegate void TypedClientConsumerMethod<in T>(NetworkChannel channel, T message) where T : ISerializable;
            public delegate void ReaderConsumerMethod(NetworkChannel channel, NetworkReader reader, ulong sender, bool isServer);
            public delegate void ReaderServerConsumerMethod(NetworkChannel channel, NetworkReader reader, ulong sender);
            public delegate void ReaderClientConsumerMethod(NetworkChannel channel, NetworkReader reader);

            /// <summary>
            /// The ID of the message being built
            /// </summary>
            private readonly int _id;
            
            /// <summary>
            /// The state of the factory.
            /// </summary>
            private FactoryState _state;
            
            /// <summary>
            /// The associated type.
            /// </summary>
            private Type _associatedType;

            /// <summary>
            /// The channel this factory belongs to.
            /// </summary>
            private readonly NetworkChannel _channel;

            /// <summary>
            /// The underlying message being constructed.
            /// </summary>
            private BaseMessage _message;
            
            /// <summary>
            /// Create a new message factory.
            /// </summary>
            /// <param name="channel">The channel the factory belongs to</param>
            /// <param name="id">The message ID</param>
            internal MessageFactory(NetworkChannel channel, int id) {
                _channel = channel;
                _id = id;
            }

            /// <summary>
            /// Add a consumer that takes the serializable object as a parameter.
            /// </summary>
            /// <param name="method">The consumer method to be added.</param>
            /// <typeparam name="T">The object type to be received. Must be compatible.</typeparam>
            /// <returns></returns>
            public MessageFactory Consumer<T>(TypedConsumerMethod<T> method) where T : ISerializable {
                if (_state != FactoryState.Invalid && _state != FactoryState.TypedConsumer)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.TypedConsumer)
                    Debug.LogWarning("Overwriting message consumer.");
                if (!SupportClient<T>() && !SupportServer<T>())
                    throw new Exception("Object based message must be given Client and Server Packet Attributes!");
                _message = new ConsumableMessage<T>(_id, method);
                _state = FactoryState.TypedConsumer;
                _associatedType = typeof(T);
                return this;
            }
            
            /// <summary>
            /// Adds a consumer for the server that takes the serializable object as a parameter.
            /// Cannot be mixed with reader consumers.
            /// </summary>
            /// <param name="method">The consumer method to be added.</param>
            /// <typeparam name="T">The object type to be received. Must be compatible.</typeparam>
            /// <returns></returns>
            /// <exception cref="Exception"></exception>
            public MessageFactory ServerConsumer<T>(TypedServerConsumerMethod<T> method) where T : ISerializable {
                if (_state != FactoryState.Invalid && _state != FactoryState.SidedTypedConsumer)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.SidedTypedConsumer && ((SidedConsumableMessage<T>) _message).typedServerMethod != null)
                    Debug.LogWarning("Overwriting message consumer.");
                if (_associatedType != null && _associatedType != typeof(T))
                    throw new Exception("Cannot change associated type!");
                if (!SupportServer<T>())
                    throw new Exception("Object based message must be given Server Packet Attribute!");
                if (_state == FactoryState.Invalid) {
                    _message = new SidedConsumableMessage<T>(_id);
                }

                _state = FactoryState.SidedTypedConsumer;
                ((SidedConsumableMessage<T>) _message).typedServerMethod = method;
                _associatedType = typeof(T);
                return this;
            }
            
            /// <summary>
            /// Adds a consumer for the client that takes the serializable object as a parameter.
            /// Cannot be mixed with reader consumers.
            /// </summary>
            /// <param name="method">The consumer method to be added.</param>
            /// <typeparam name="T">The object type to be received. Must be compatible.</typeparam>
            /// <returns></returns>
            /// <exception cref="Exception"></exception>
            public MessageFactory ClientConsumer<T>(TypedClientConsumerMethod<T> method) where T : ISerializable {
                if (_state != FactoryState.Invalid && _state != FactoryState.SidedTypedConsumer)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.SidedTypedConsumer && ((SidedConsumableMessage<T>) _message).typedClientMethod != null)
                    Debug.LogWarning("Overwriting message consumer.");
                if (_associatedType != null && _associatedType != typeof(T))
                    throw new Exception("Cannot change associated type!");
                if (!SupportClient<T>())
                    throw new Exception("Object based message must be given Client Packet Attribute!");
                
                if (_state == FactoryState.Invalid) {
                    _message = new SidedConsumableMessage<T>(_id);
                }

                _state = FactoryState.SidedTypedConsumer;
                ((SidedConsumableMessage<T>) _message).typedClientMethod = method;
                _associatedType = typeof(T);
                return this;
            }

            /// <summary>
            /// Adds a consumer that takes a reader.
            /// </summary>
            /// <param name="consumerMethod">The consumer method.</param>
            /// <returns></returns>
            /// <exception cref="Exception"></exception>
            public MessageFactory Consumer(ReaderConsumerMethod consumerMethod) {
                if (_state != FactoryState.Invalid && _state != FactoryState.ReaderConsumer)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.ReaderConsumer)
                    Debug.LogWarning("Overwriting message consumer.");

                _state = FactoryState.ReaderConsumer;
                _message = new ReaderMessage(_id, consumerMethod);
                return this;
            }
            
            /// <summary>
            /// Adds a server consumer that takes a reader.
            /// Cannot be mixed with typed consumers.
            /// </summary>
            /// <param name="consumerMethod">The consumer method.</param>
            /// <returns></returns>
            /// <exception cref="Exception"></exception>
            public MessageFactory ServerConsumer(ReaderServerConsumerMethod consumerMethod) {
                if (_state != FactoryState.Invalid && _state != FactoryState.SidedTypedConsumer)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.SidedTypedConsumer && ((SidedReaderMessage) _message).serverConsumerMethod != null)
                    Debug.LogWarning("Overwriting message consumer.");
                
                if (_state == FactoryState.Invalid) {
                    _message = new SidedReaderMessage(_id);
                }

                _state = FactoryState.SidedTypedConsumer;
                ((SidedReaderMessage) _message).serverConsumerMethod = consumerMethod;
                return this;
            }
            
            /// <summary>
            /// Adds a client consumer that takes a reader.
            /// Cannot be mixed with typed consumers.
            /// </summary>
            /// <param name="consumerMethod">The consumer method.</param>
            /// <returns></returns>
            /// <exception cref="Exception"></exception>
            public MessageFactory ClientConsumer(ReaderClientConsumerMethod consumerMethod) {
                if (_state != FactoryState.Invalid && _state != FactoryState.SidedTypedConsumer)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.SidedTypedConsumer && ((SidedReaderMessage) _message).clientConsumerMethod != null)
                    Debug.LogWarning("Overwriting message consumer.");

                if (_state == FactoryState.Invalid) {
                    _message = new SidedReaderMessage(_id);
                }

                _state = FactoryState.SidedTypedConsumer;
                ((SidedReaderMessage) _message).clientConsumerMethod = consumerMethod;
                return this;
            }

            /// <summary>
            /// Denotes whether or not the message can be buffered
            /// </summary>
            /// <returns></returns>
            /// <exception cref="Exception"></exception>
            public MessageFactory Buffered() {
                if (_state == FactoryState.Invalid)
                    throw new Exception("Bufferable must be called just before Register");
                if (_state == FactoryState.ReaderConsumer || _state == FactoryState.SidedReaderConsumer)
                    throw new Exception("Reader messages do not support buffering!"); // We do not support this as we'd have to copy out memory to somewhere else every time.
                _message.canBuffer = true;
                return this;
            }

            /// <summary>
            /// Register the message in the channel.
            /// </summary>
            /// <exception cref="Exception">Factory is incomplete.</exception>
            public void Register() {
                if (_state == FactoryState.Invalid)
                    throw new Exception("Factory is not complete!");
                _channel._messages.Add(_id, _message);

                if (_state == FactoryState.TypedConsumer || _state == FactoryState.SidedTypedConsumer)
                    _channel._typedMessages.Add(_associatedType, _message);
            }
        }

        /// <summary>
        /// Default reliable channel.
        /// </summary>
        public static readonly NetworkChannel Reliable = new NetworkChannel(ChannelType.Reliable);
        
        /// <summary>
        /// Default reliable sequenced channel.
        /// </summary>
        public static readonly NetworkChannel ReliableSequenced = new NetworkChannel(ChannelType.ReliableSequenced);
        
        /// <summary>
        /// Default unreliable channel.
        /// </summary>
        public static readonly NetworkChannel Unreliable = new NetworkChannel(ChannelType.Unreliable);

        /// <summary>
        /// The channel type.
        /// Used for URDP transports.
        /// </summary>
        public ChannelType channelType;
        
        /// <summary>
        /// Internal ID counter.
        /// </summary>
        private int _idCounter;

        /// <summary>
        /// Get the channel ID.
        /// Mostly for internal use.
        /// </summary>
        public int ID {
            get {
                if (NetworkManager.instance == null)
                    return -1;
                return NetworkManager.instance.networkConfig.channels.IndexOf(this);
            }
        }

        /// <summary>
        /// Create a new network channel.
        /// </summary>
        /// <param name="type">Network channel type.</param>
        public NetworkChannel(ChannelType type) {
            channelType = type;
        }

        /// <summary>
        /// Index based list of messages.
        /// </summary>
        private Dictionary<int, BaseMessage> _messages = new Dictionary<int, BaseMessage>();
        
        /// <summary>
        /// Type based list of messages.
        /// </summary>
        private Dictionary<Type, BaseMessage> _typedMessages = new Dictionary<Type,BaseMessage>();

        /// <summary>
        /// Get the next available ID from the internal counter.
        /// </summary>
        /// <returns>The next available ID</returns>
        public int GetNextID() {
            return _idCounter++;
        }

        /// <summary>
        /// Handle an incoming packet.
        /// </summary>
        /// <param name="clientID"></param>
        /// <param name="reader"></param>
        /// <param name="server"></param>
        public void HandleIncoming(ulong clientID, NetworkReader reader, bool server) {
            // Get message ID.
            var messageID = reader.ReadPackedInt32();
            if (_messages.TryGetValue(messageID, out var msg)) {
                msg.Invoke(this, clientID, reader, server);
            } else {
                Debug.LogWarning("Invalid message received.");
            }
        }

        /// <summary>
        /// Handle a buffered packet.
        /// </summary>
        /// <param name="packet">The buffered packet</param>
        /// <param name="server">Whether or not this is handled on serverside.</param>
        public void HandleBuffered(BufferedPacket packet, bool server) {
            if (_typedMessages.TryGetValue(packet.packet.GetType(), out var msg)) {
                msg.InvokeBuffered(packet, server);
            } else {
                Debug.LogWarning("Unable to find this message type in the message registry.");
            }
        }
        
        /// <summary>
        /// Get a factory for the next message ID.
        /// </summary>
        /// <returns></returns>
        public MessageFactory GetFactory() {
            return new MessageFactory(this, GetNextID());
        }

        /// <summary>
        /// Get a factory for the current ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public MessageFactory GetFactory(int id) {
            return new MessageFactory(this, id);
        }
        
        /// <summary>
        /// Single sender list. Used for sending data to one client using the same method as to many.
        /// </summary>
        private List<ulong> _singleSenderList = new List<ulong> {0};

        /// <summary>
        /// The current transport.
        /// </summary>
        private Transport Transport {
            get {
                if (NetworkManager.instance == null)
                    return null;
                return NetworkManager.instance.networkConfig.transport;
            }
        }

        /// <summary>
        /// Send a generic message to a client.
        /// </summary>
        /// <param name="clientId">The client to send to.</param>
        /// <param name="messageId">The message ID.</param>
        /// <param name="packetWriter">Writer containing message information.</param>
        /// <exception cref="Exception"></exception>
        public void ServerSend(ulong clientId, int messageId, NetworkWriter packetWriter) {
            if (!_messages.ContainsKey(messageId))
                throw new Exception("Message ID is not in registry!");

            using (var writer = NetworkWriterPool.GetWriter()) {
                // Write message ID
                writer.WritePackedInt32(messageId);
                
                // Write data from writer
                writer.WriteSegmentWithSize(packetWriter.ToArraySegment());

                _singleSenderList[0] = clientId;
                Transport.ServerSend(_singleSenderList, writer.ToArraySegment(), ID);
            }
        }

        /// <summary>
        /// Send a typed message to a client.
        /// </summary>
        /// <param name="clientId">The client to send to.</param>
        /// <param name="packet">The message to send.</param>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <exception cref="Exception">Message type not in the registry.</exception>
        public void ServerSend<T>(ulong clientId, T packet) where T : ISerializable {
            if (_typedMessages.TryGetValue(typeof(T), out var msg)) {
                using (var writer = NetworkWriterPool.GetWriter()) {
                    // Write message ID
                    writer.WritePackedInt32(msg.id);
                
                    // Write data from object
                    writer.WritePackedObject(packet);

                    _singleSenderList[0] = clientId;
                    Transport.ServerSend(_singleSenderList, writer.ToArraySegment(), ID);
                }
            } else {
                throw new Exception("This message type is not present in the registry!");
            }
        }
        
        /// <summary>
        /// Send a generic message to a list of clients.
        /// </summary>
        /// <param name="clientIds">The clients to send to.</param>
        /// <param name="messageId">The message ID.</param>
        /// <param name="packetWriter">Writer containing message information.</param>
        /// <exception cref="Exception"></exception>
        public void ServerSend(List<ulong> clientIds, int messageId, NetworkWriter packetWriter) {
            if (_messages.ContainsKey(messageId)) {
                using (var writer = NetworkWriterPool.GetWriter()) {
                    // Write message ID
                    writer.WritePackedInt32(messageId);
                
                    // Write data from writer
                    writer.WriteSegmentWithSize(packetWriter.ToArraySegment());

                    Transport.ServerSend(clientIds, writer.ToArraySegment(), ID);
                }
            } else {
                throw new Exception("This message type is not present in the registry!");
            }
        }

        /// <summary>
        /// Send a typed message to a list of clients.
        /// </summary>
        /// <param name="clientIds">The clients to send to.</param>
        /// <param name="packet">The message to send.</param>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <exception cref="Exception"></exception>
        public void ServerSend<T>(List<ulong> clientIds, T packet) where T : ISerializable {
            if (_typedMessages.TryGetValue(typeof(T), out var msg)) {
                using (var writer = NetworkWriterPool.GetWriter()) {
                    // Write message ID
                    writer.WritePackedInt32(msg.id);
                
                    // Write data from object
                    writer.WritePackedObject(packet);

                    Transport.ServerSend(clientIds, writer.ToArraySegment(), ID);
                }
            } else {
                throw new Exception("This message type is not present in the registry!");
            }
        }
        
        /// <summary>
        /// Send a generic message to a list of clients, excluding one.
        /// </summary>
        /// <param name="clientIds">The clients to send to.</param>
        /// <param name="messageId">The message ID.</param>
        /// <param name="packetWriter">Writer containing message information.</param>
        /// <param name="excludedClient">The client to exclude.</param>
        /// <exception cref="Exception"></exception>
        public void ServerSend(List<ulong> clientIds, int messageId, NetworkWriter packetWriter, ulong excludedClient) {
            if (_messages.ContainsKey(messageId)) {
                using (var writer = NetworkWriterPool.GetWriter()) {
                    // Write message ID
                    writer.WritePackedInt32(messageId);
                
                    // Write data from writer
                    writer.WriteSegmentWithSize(packetWriter.ToArraySegment());

                    Transport.ServerSend(clientIds, writer.ToArraySegment(), ID, excludedClient);
                }
            } else {
                throw new Exception("This message type is not present in the registry!");
            }
        }

        /// <summary>
        /// Sends a typed message to a list of clients, excluding one.
        /// </summary>
        /// <param name="clientIds">The clients to send to.</param>
        /// <param name="packet">The message to send.</param>
        /// <param name="excludedClient">The client to exclude.</param>
        /// <typeparam name="T">The type of the message</typeparam>
        /// <exception cref="Exception"></exception>
        public void ServerSend<T>(List<ulong> clientIds, T packet, ulong excludedClient) where T : ISerializable {
            if (_typedMessages.TryGetValue(typeof(T), out var msg)) {
                using (var writer = NetworkWriterPool.GetWriter()) {
                    // Write message ID
                    writer.WritePackedInt32(msg.id);
                
                    // Write data from object
                    writer.WritePackedObject(packet);

                    Transport.ServerSend(clientIds, writer.ToArraySegment(), ID, excludedClient);
                }
            } else {
                throw new Exception("This message type is not present in the registry!");
            }
        }
        
        /// <summary>
        /// Send a generic message to all clients.
        /// </summary>
        /// <param name="messageId">The message ID.</param>
        /// <param name="packetWriter">Writer containing message information</param>
        public void ServerSend(int messageId, NetworkWriter packetWriter) {
            ServerSend(NetworkManager.instance.allClientIDs, messageId, packetWriter);
        }

        /// <summary>
        /// Send a typed message to all clients.
        /// </summary>
        /// <param name="packet">The message to send.</param>
        /// <typeparam name="T">The type of the message</typeparam>
        public void ServerSend<T>(T packet) where T : ISerializable {
            ServerSend(NetworkManager.instance.allClientIDs, packet);
        }

        /// <summary>
        /// Send a generic message to all clients excluding one.
        /// </summary>
        /// <param name="messageId">The message ID.</param>
        /// <param name="packetWriter">Writer containing message information</param>
        /// <param name="excludedClient">Client to exclude.</param>
        public void ServerSend(int messageId, NetworkWriter packetWriter, ulong excludedClient) {
            ServerSend(NetworkManager.instance.allClientIDs, messageId, packetWriter, excludedClient);
        }

        /// <summary>
        /// Send a typed message to all clients excluding one.
        /// </summary>
        /// <param name="packet">The message to send.</param>
        /// <param name="excludedClient">Client to exclude</param>
        /// <typeparam name="T">The type of the message</typeparam>
        public void ServerSend<T>(T packet, ulong excludedClient) where T : ISerializable {
            ServerSend(NetworkManager.instance.allClientIDs, packet, excludedClient);
        }
        
        /// <summary>
        /// Send a generic message to the server.
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="packetWriter"></param>
        public void ClientSend(int messageId, NetworkWriter packetWriter) {
            if (_messages.ContainsKey(messageId)) {
                // Host mode will not send data to the server
                if (NetworkManager.instance.isHost)
                    return;

                // Write the data and send it with the transport
                using (var writer = NetworkWriterPool.GetWriter()) {
                    // Write ID
                    writer.WritePackedInt32(messageId);

                    // Write packet.
                    writer.WriteSegmentWithSize(packetWriter.ToArraySegment());

                    // Send with transport
                    Transport.ClientSend(writer.ToArraySegment(), ID);
                }
            } else {
                throw new Exception("This message type is not present in the registry!");
            }
        }

        /// <summary>
        /// Send a typed message to the server.
        /// </summary>
        /// <param name="packet">The message to send.</param>
        /// <typeparam name="T">The type of the message.</typeparam>
        /// <exception cref="Exception">The type was not found in the registry.</exception>
        public void ClientSend<T>(T packet) where T : ISerializable {
            if (_typedMessages.TryGetValue(typeof(T), out var msg)) {
                // Host mode will not send data to the server
                if (NetworkManager.instance.isHost)
                    return;

                // Write the data and send it with the transport
                using (var writer = NetworkWriterPool.GetWriter()) {
                    // Write ID
                    writer.WritePackedInt32(msg.id);

                    // Write packet.
                    writer.WritePackedObject(packet);

                    // Send with transport
                    Transport.ClientSend(writer.ToArraySegment(), ID);
                }
            } else {
                throw new Exception("This message type is not present in the registry!");
            }
        }
        
        /// <summary>
        /// Fully reset channel, clearing registries.
        /// </summary>
        public void ResetChannel() {
            _messages.Clear();
            _typedMessages.Clear();
            _idCounter = 0;
        }
        
        private abstract class BaseMessage {
            public readonly int id;
            public bool canBuffer;

            protected BaseMessage(int id) {
                this.id = id;
            }

            public abstract void Invoke(NetworkChannel channel, ulong sender, NetworkReader reader, bool server);

            public virtual void InvokeBuffered(BufferedPacket packet, bool server) {
                throw new NotSupportedException();
            }
        }

        private class ConsumableMessage<T> : BaseMessage where T : ISerializable {
            private MessageFactory.TypedConsumerMethod<T> _method;

            public ConsumableMessage(int id, MessageFactory.TypedConsumerMethod<T> method) : base(id) {
                _method = method;
            }
            
            public override void Invoke(NetworkChannel channel, ulong sender, NetworkReader reader, bool server) {
                var packet = reader.ReadPackedObject<T>();

                if (canBuffer) {
                    if (packet is IBufferablePacket bufferablePacket) {
                        if (bufferablePacket.BufferPacket(channel, sender))
                            return;
                    }
                }
                
                if ((server && SupportServer<T>()) || (!server && SupportClient<T>()))
                    _method.Invoke(channel, packet, sender, server);
            }

            public override void InvokeBuffered(BufferedPacket packet, bool server) {
                if ((server && SupportServer<T>()) || (!server && SupportClient<T>()))
                    _method.Invoke(packet.channel, (T) packet.packet, packet.sender, server);
            }
        }
        
        private class SidedConsumableMessage<T> : BaseMessage where T : ISerializable {
            public MessageFactory.TypedServerConsumerMethod<T> typedServerMethod;
            public MessageFactory.TypedClientConsumerMethod<T> typedClientMethod;

            public SidedConsumableMessage(int id) : base(id) { }

            public override void Invoke(NetworkChannel channel, ulong sender, NetworkReader reader, bool server) {
                var packet = reader.ReadPackedObject<T>();

                if (canBuffer) {
                    if (packet is IBufferablePacket bufferablePacket) {
                        if (bufferablePacket.BufferPacket(channel, sender))
                            return;
                    }
                }

                if (server && SupportServer<T>()) {
                    typedServerMethod?.Invoke(channel, packet, sender);
                } else if (!server && SupportClient<T>()) {
                    typedClientMethod?.Invoke(channel, packet);
                } else {
                    Debug.LogWarning("Ignoring unsupported packet.");
                }
            }
            
            public override void InvokeBuffered(BufferedPacket packet, bool server) {
                if (server && SupportServer<T>()) {
                    typedServerMethod?.Invoke(packet.channel, (T) packet.packet, packet.sender);
                } else if (!server && SupportClient<T>()) {
                    typedClientMethod?.Invoke(packet.channel, (T) packet.packet);
                } else {
                    Debug.LogWarning("Ignoring unsupported packet.");
                }
            }
        }
        
        private class ReaderMessage : BaseMessage {
            private readonly MessageFactory.ReaderConsumerMethod _consumerMethod;

            public ReaderMessage(int id, MessageFactory.ReaderConsumerMethod consumerMethod) : base(id) {
                _consumerMethod = consumerMethod;
            }
            
            public override void Invoke(NetworkChannel channel, ulong clientID, NetworkReader reader, bool server) {
                _consumerMethod?.Invoke(channel, reader, clientID, server);
            }
        }
        
        private class SidedReaderMessage : BaseMessage {
            public MessageFactory.ReaderServerConsumerMethod serverConsumerMethod;
            public MessageFactory.ReaderClientConsumerMethod clientConsumerMethod;

            public SidedReaderMessage(int id) : base(id) { }

            public override void Invoke(NetworkChannel channel, ulong sender, NetworkReader reader, bool server) {
                if (server)
                    serverConsumerMethod?.Invoke(channel, reader, sender);
                else clientConsumerMethod?.Invoke(channel, reader);
            }
        }
        
        internal static bool SupportClient<T>() {
            return typeof(T).GetCustomAttributes(typeof(ClientPacketAttribute), false).Length > 0;
        }
        
        internal static bool SupportServer<T>() {
            return typeof(T).GetCustomAttributes(typeof(ServerPacketAttribute), false).Length > 0;
        }
    }
}