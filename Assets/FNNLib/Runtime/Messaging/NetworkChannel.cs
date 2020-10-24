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
                Consumer,
                SidedConsumer,
                Reader,
                SidedReader
            }
            
            public delegate void ConsumerMethod<in T>(NetworkChannel channel, T message, ulong sender, bool isServer) where T : ISerializable;
            public delegate void ServerConsumerMethod<in T>(NetworkChannel channel, T message, ulong sender) where T : ISerializable;
            public delegate void ClientConsumerMethod<in T>(NetworkChannel channel, T message) where T : ISerializable;
            public delegate void ReaderMethod(NetworkChannel channel, NetworkReader reader, ulong sender);

            private readonly int _id;
            private FactoryState _state;
            private Type _associatedType;

            private readonly NetworkChannel _channel;

            private BaseMessage _message;
            
            /// <summary>
            /// Create a new message factory.
            /// </summary>
            /// <param name="channel"></param>
            /// <param name="id"></param>
            public MessageFactory(NetworkChannel channel, int id) {
                _channel = channel;
                _id = id;
            }

            /// <summary>
            /// Add a consumer that takes the serializable object as a parameter.
            /// This must be a compatible object!
            /// </summary>
            /// <param name="method"></param>
            /// <returns></returns>
            public MessageFactory Consumer<T>(ConsumerMethod<T> method) where T : ISerializable {
                if (_state != FactoryState.Invalid && _state != FactoryState.Consumer)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.Consumer)
                    Debug.LogWarning("Overwriting message consumer.");
                if (!SupportClient<T>() && !SupportServer<T>())
                    throw new Exception("Object based message must be given Client and Server Packet Attributes!");
                _message = new ConsumableMessage<T>(_id, method);
                _state = FactoryState.Consumer;
                _associatedType = typeof(T);
                return this;
            }
            
            public MessageFactory ServerConsumer<T>(ServerConsumerMethod<T> method) where T : ISerializable {
                if (_state != FactoryState.Invalid && _state != FactoryState.SidedConsumer)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.SidedConsumer && ((SidedConsumableMessage<T>) _message).serverMethod != null)
                    Debug.LogWarning("Overwriting message consumer.");
                if (_associatedType != null && _associatedType != typeof(T))
                    throw new Exception("Cannot change associated type!");
                if (!SupportServer<T>())
                    throw new Exception("Object based message must be given Server Packet Attribute!");
                if (_state == FactoryState.Invalid) {
                    _message = new SidedConsumableMessage<T>(_id);
                }

                _state = FactoryState.SidedConsumer;
                ((SidedConsumableMessage<T>) _message).serverMethod = method;
                _associatedType = typeof(T);
                return this;
            }
            
            public MessageFactory ClientConsumer<T>(ClientConsumerMethod<T> method) where T : ISerializable {
                if (_state != FactoryState.Invalid && _state != FactoryState.SidedConsumer)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.SidedConsumer && ((SidedConsumableMessage<T>) _message).clientMethod != null)
                    Debug.LogWarning("Overwriting message consumer.");
                if (_associatedType != null && _associatedType != typeof(T))
                    throw new Exception("Cannot change associated type!");
                if (!SupportClient<T>())
                    throw new Exception("Object based message must be given Client Packet Attribute!");
                
                if (_state == FactoryState.Invalid) {
                    _message = new SidedConsumableMessage<T>(_id);
                }

                _state = FactoryState.SidedConsumer;
                ((SidedConsumableMessage<T>) _message).clientMethod = method;
                _associatedType = typeof(T);
                return this;
            }

            public MessageFactory Reader(ReaderMethod method) {
                if (_state != FactoryState.Invalid && _state != FactoryState.Reader)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.Reader)
                    Debug.LogWarning("Overwriting message consumer.");

                _state = FactoryState.Reader;
                _message = new ReaderMessage(_id, method);
                return this;
            }
            
            public MessageFactory ServerReader(ReaderMethod method) {
                if (_state != FactoryState.Invalid && _state != FactoryState.SidedConsumer)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.SidedConsumer && ((SidedReaderMessage) _message).serverMethod != null)
                    Debug.LogWarning("Overwriting message consumer.");
                
                if (_state == FactoryState.Invalid) {
                    _message = new SidedReaderMessage(_id);
                }

                _state = FactoryState.SidedConsumer;
                ((SidedReaderMessage) _message).serverMethod = method;
                return this;
            }
            
            public MessageFactory ClientReader(ReaderMethod method) {
                if (_state != FactoryState.Invalid && _state != FactoryState.SidedConsumer)
                    throw new Exception("Another mode is already enabled!");
                if (_state == FactoryState.SidedConsumer && ((SidedReaderMessage) _message).clientMethod != null)
                    Debug.LogWarning("Overwriting message consumer.");

                if (_state == FactoryState.Invalid) {
                    _message = new SidedReaderMessage(_id);
                }

                _state = FactoryState.SidedConsumer;
                ((SidedReaderMessage) _message).clientMethod = method;
                return this;
            }

            public MessageFactory Bufferable() {
                if (_state == FactoryState.Invalid)
                    throw new Exception("Bufferable must be called just before Register");
                if (_state == FactoryState.Reader || _state == FactoryState.SidedReader)
                    throw new Exception("Reader messages do not support buffering!");
                _message.bufferable = true;
                return this;
            }

            public void Register() {
                if (_state == FactoryState.Invalid)
                    throw new Exception("Builder is not complete!");
                _channel._messages.Add(_id, _message);

                if (_state == FactoryState.Consumer || _state == FactoryState.SidedConsumer)
                    _channel._typedMessages.Add(_associatedType, _message);
            }

            private static bool SupportClient<T>() {
                return typeof(T).GetCustomAttributes(typeof(ClientPacketAttribute), false).Length > 0;
            }
            
            public static bool SupportServer<T>() {
                return typeof(T).GetCustomAttributes(typeof(ServerPacketAttribute), false).Length > 0;
            }
        }

        private abstract class BaseMessage {
            public int id;
            public bool bufferable;

            public BaseMessage(int id) {
                this.id = id;
            }

            public abstract void Invoke(NetworkChannel channel, ulong sender, NetworkReader reader, bool server);

            public virtual void InvokeBuffered(BufferedPacket packet, bool server) {
                if (bufferable)
                    throw new NotImplementedException();
                throw new NotSupportedException();
            }
        }

        private class ConsumableMessage<T> : BaseMessage where T : ISerializable {
            private MessageFactory.ConsumerMethod<T> _method;

            public ConsumableMessage(int id, MessageFactory.ConsumerMethod<T> method) : base(id) {
                _method = method;
            }
            
            public override void Invoke(NetworkChannel channel, ulong sender, NetworkReader reader, bool server) {
                var packet = reader.ReadPackedObject<T>();

                if (bufferable) {
                    if (packet is IBufferablePacket bufferablePacket) {
                        if (bufferablePacket.BufferPacket(channel, sender))
                            return;
                    }
                }
                
                _method.Invoke(channel, packet, sender, server);
            }

            public override void InvokeBuffered(BufferedPacket packet, bool server) {
                _method.Invoke(packet.channel, (T) packet.packet, packet.sender, server);
            }
        }
        
        private class SidedConsumableMessage<T> : BaseMessage where T : ISerializable {
            public MessageFactory.ServerConsumerMethod<T> serverMethod;
            public MessageFactory.ClientConsumerMethod<T> clientMethod;

            public SidedConsumableMessage(int id) : base(id) { }

            public override void Invoke(NetworkChannel channel, ulong sender, NetworkReader reader, bool server) {
                var packet = reader.ReadPackedObject<T>();

                if (bufferable) {
                    if (packet is IBufferablePacket bufferablePacket) {
                        if (bufferablePacket.BufferPacket(channel, sender))
                            return;
                    }
                }
                if (server)
                    serverMethod?.Invoke(channel, packet, sender);
                else clientMethod?.Invoke(channel, packet);
            }
            
            public override void InvokeBuffered(BufferedPacket packet, bool server) {
                if (server)
                    serverMethod?.Invoke(packet.channel, (T) packet.packet, packet.sender);
                else clientMethod?.Invoke(packet.channel, (T) packet.packet);
            }
        }
        
        private class ReaderMessage : BaseMessage {
            private MessageFactory.ReaderMethod _method;

            public ReaderMessage(int id, MessageFactory.ReaderMethod method) : base(id) {
                _method = method;
            }
            
            public override void Invoke(NetworkChannel channel, ulong clientID, NetworkReader reader, bool server) {
                _method?.Invoke(channel, reader, clientID);
            }
        }
        
        private class SidedReaderMessage : BaseMessage {
            public MessageFactory.ReaderMethod serverMethod;
            public MessageFactory.ReaderMethod clientMethod;

            public SidedReaderMessage(int id) : base(id) { }

            public override void Invoke(NetworkChannel channel, ulong sender, NetworkReader reader, bool server) {
                if (server)
                    serverMethod?.Invoke(channel, reader, sender);
                else clientMethod?.Invoke(channel, reader, sender);
            }
        }

        /// <summary>
        /// The channel type.
        /// Used for URDP transports.
        /// </summary>
        public ChannelType channelType;
        private int _idCounter;

        public int ID {
            get {
                if (NetworkManager.instance == null)
                    return -1;
                return NetworkManager.instance.channels.IndexOf(this);
            }
        }

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
        /// <returns></returns>
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
        
        // TODO: All kinds of server send. This is a prototype at the moment
        
        private List<ulong> _singleSenderList = new List<ulong> {0};

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
        /// <param name="clientId"></param>
        /// <param name="messageId"></param>
        /// <param name="writer"></param>
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
        
        public void ServerSend(int messageId, NetworkWriter packetWriter) {
            ServerSend(NetworkManager.instance.allClientIDs, messageId, packetWriter);
        }

        public void ServerSend<T>(T packet) where T : ISerializable {
            ServerSend(NetworkManager.instance.allClientIDs, packet);
        }
        
        public void ServerSend(int messageId, NetworkWriter packetWriter, ulong excludedClient) {
            ServerSend(NetworkManager.instance.allClientIDs, messageId, packetWriter, excludedClient);
        }

        public void ServerSend<T>(T packet, ulong excludedClient) where T : ISerializable {
            ServerSend(NetworkManager.instance.allClientIDs, packet, excludedClient);
        }
        
        /// <summary>
        /// Send a generic message to the server.
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="messageId"></param>
        /// <param name="writer"></param>
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
        
        // Default channels
        public static readonly NetworkChannel Reliable = new NetworkChannel(ChannelType.Reliable);
        public static readonly NetworkChannel ReliableSequenced = new NetworkChannel(ChannelType.ReliableSequenced);
        public static readonly NetworkChannel Unreliable = new NetworkChannel(ChannelType.Unreliable);
        // public static NetworkChannel ReliableSequenced => Reliable; // TODO: Own channel
    }
}