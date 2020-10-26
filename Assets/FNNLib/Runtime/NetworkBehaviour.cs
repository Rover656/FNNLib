﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FNNLib.Config;
using FNNLib.Messaging;
using FNNLib.ReplicatedVar;
using FNNLib.RPC;
using FNNLib.SceneManagement;
using FNNLib.Serialization;
using FNNLib.Spawning;
using FNNLib.Utilities;
using UnityEngine;

namespace FNNLib {
    public abstract partial class NetworkBehaviour : MonoBehaviour {
        #region Identity Fetch

        private NetworkIdentity _identity;

        public NetworkIdentity identity {
            get {
                if (_identity == null)
                    _identity = GetComponentInParent<NetworkIdentity>();
                return _identity;
            }
        }

        public bool hasIdentity => identity != null;

        #endregion

        #region Identity Passthrough

        public uint networkSceneID => identity.networkSceneID;

        public NetworkScene networkScene => identity.networkScene;

        public ulong networkID => identity.networkID;

        public bool isSpawned => identity.isSpawned;

        public bool isLocalPlayer => identity.isLocalPlayer;

        public ulong ownerClientID => identity.ownerClientID;

        public bool isOwner => identity.isOwner;

        public bool isOwnedByServer => identity.isOwnedByServer;

        #endregion

        /// <summary>
        /// The client that is current executing an RPC.
        /// </summary>
        protected ulong executingRPCSender => _executingRPCSender;

        internal ulong _executingRPCSender;

        /// <summary>
        /// Is running in a server context?
        /// </summary>
        public bool isServer => NetworkManager.instance.isServer;

        /// <summary>
        /// Is running in a client context?
        /// </summary>
        public bool isClient => NetworkManager.instance.isClient;

        public bool isHost => NetworkManager.instance.isHost;

        #region Behaviour Identification

        public int behaviourIndex {
            get {
                if (identity == null)
                    return -1;
                return identity.behaviours.IndexOf(this);
            }
        }

        #endregion

        #region Network Lifecycle

        internal bool netStartInvoked;
        internal bool internalNetStartInvoked;

        public virtual void NetworkStart() { }

        internal void InternalNetworkStart() {
            _rpcReflector = RPCReflector.GetOrCreate(GetType());
            rpcDelegates = _rpcReflector.CreateTargetDelegates(this);
            _varReflector = new VarReflector(GetType(), this);
        }

        #endregion

        #region RPCs

        private RPCReflector _rpcReflector;
        internal RPCDelegate[] rpcDelegates;

        internal void SendClientRPCCall(ulong hash, List<ulong> clients, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                if (isHost && identity.observers.Contains(NetworkManager.instance.localClientID) &&
                    clients.Contains(NetworkManager.instance.localClientID))
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment()))
                        InvokeClientRPCLocal(hash, reader);

                var packet = new RPCPacket {
                                               behaviourOrder = behaviourIndex,
                                               methodHash = hash,
                                               networkID = networkID,
                                               parameterBuffer = writer.ToArraySegment()
                                           };
                NetworkChannel.ReliableSequenced.ServerSend(clients, packet);
            }
        }

        internal void SendClientRPCCallOn(ulong hash, ulong client, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                // If we're the host, ignore sending the packet.
                if (isHost && client == NetworkManager.instance.localClientID)
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeClientRPCLocal(hash, reader);
                    }
                else {
                    var packet = new RPCPacket {
                                                   behaviourOrder = behaviourIndex,
                                                   methodHash = hash,
                                                   networkID = networkID,
                                                   parameterBuffer = writer.ToArraySegment()
                                               };
                    NetworkChannel.ReliableSequenced.ServerSend(client, packet);
                }
            }
        }
        
        internal RPCResponse<T> SendClientRPCCallOnResponse<T>(ulong hash, ulong client, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");
            
            // Get response id
            var responseID = RPCResponseManager.GetResponseID();

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                // If we're the host, ignore sending the packet.
                if (isHost && client == NetworkManager.instance.localClientID)
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        var result = InvokeClientRPCLocal(hash, reader);
                        return new RPCResponse<T> {
                                                      clientID = NetworkManager.instance.localClientID,
                                                      isDone = true,
                                                      isSuccessful = true,
                                                      responseID = responseID,
                                                      result = result
                                                  };
                    }
                
                // Construct packet
                var packet = new RPCPacket {
                                               behaviourOrder = behaviourIndex,
                                               methodHash = hash,
                                               networkID = networkID,
                                               parameterBuffer = writer.ToArraySegment(),
                                               expectsResponse = true,
                                               responseID = responseID
                                           };
                
                // Build response
                var response = new RPCResponse<T> {
                                                      clientID = NetworkManager.instance.localClientID,
                                                      isDone = false,
                                                      isSuccessful = false,
                                                      responseID = responseID,
                                                      resultType = typeof(T)
                                                  };
                    
                // Add to response manager
                RPCResponseManager.Add(responseID, response);
                    
                // Send packet
                NetworkChannel.ReliableSequenced.ServerSend(client, packet);
                return response;
            }
        }

        internal void SendClientRPCCallAll(ulong hash, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                if (isHost && identity.observers.Contains(NetworkManager.instance.localClientID))
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeClientRPCLocal(hash, reader);
                    }

                var packet = new RPCPacket {
                                               behaviourOrder = behaviourIndex,
                                               methodHash = hash,
                                               networkID = networkID,
                                               parameterBuffer = writer.ToArraySegment()
                                           };
                NetworkChannel.ReliableSequenced.ServerSend(identity.observers, packet);
            }
        }

        internal void SendClientRPCCallAllExcept(ulong hash, ulong excludedClient, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                if (isHost && identity.observers.Contains(NetworkManager.instance.localClientID))
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeClientRPCLocal(hash, reader);
                    }

                var packet = new RPCPacket {
                                               behaviourOrder = behaviourIndex,
                                               methodHash = hash,
                                               networkID = networkID,
                                               parameterBuffer = writer.ToArraySegment()
                                           };
                NetworkChannel.ReliableSequenced.ServerSend(identity.observers, packet, excludedClient);
            }
        }

        internal void SendServerRPCCall(ulong hash, params object[] args) {
            // Block non-client calls
            if (!isClient)
                throw new NotSupportedException("Only the client may invoke RPCs on the server.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                if (isHost)
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeServerRPCLocal(hash, NetworkManager.instance.localClientID, reader);
                    }
                else {
                    var packet = new RPCPacket {
                                                   behaviourOrder = behaviourIndex,
                                                   methodHash = hash,
                                                   networkID = networkID,
                                                   parameterBuffer = writer.ToArraySegment()
                                               };
                    NetworkChannel.ReliableSequenced.ClientSend(packet);
                }
            }
        }
        
        internal RPCResponse<T> SendServerRPCCallResponse<T>(ulong hash, params object[] args) {
            // Block non-client calls
            if (!isClient)
                throw new NotSupportedException("Only the client may invoke RPCs on the server.");

            // Get a response ID
            var responseID = RPCResponseManager.GetResponseID();

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                if (isHost) {
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        var result = InvokeServerRPCLocal(hash, NetworkManager.instance.localClientID, reader);
                        return new RPCResponse<T> {
                                                      clientID = NetworkManager.instance.localClientID,
                                                      isDone = true,
                                                      isSuccessful = true,
                                                      responseID = responseID,
                                                      result = result
                                                  };
                    }
                }

                // Construct call packet
                var packet = new RPCPacket {
                                               behaviourOrder = behaviourIndex,
                                               methodHash = hash,
                                               networkID = networkID,
                                               parameterBuffer = writer.ToArraySegment(),
                                               responseID = responseID,
                                               expectsResponse = true
                                           };
                    
                // Build response
                var response = new RPCResponse<T> {
                                                      clientID = NetworkManager.instance.localClientID,
                                                      isDone = false,
                                                      isSuccessful = false,
                                                      responseID = responseID,
                                                      resultType = typeof(T)
                                                  };
                    
                // Add to response manager
                RPCResponseManager.Add(responseID, response);
                    
                // Send packet
                NetworkChannel.Reliable.ClientSend(packet);
                return response;
            }
        }

        private object InvokeClientRPCLocal(ulong hash, NetworkReader args) {
            if (_rpcReflector.clientMethods.ContainsKey(hash))
                return _rpcReflector.clientMethods[hash].Invoke(this, 0, args);
            return null;
        }

        private object InvokeServerRPCLocal(ulong hash, ulong sender, NetworkReader args) {
            if (_rpcReflector.serverMethods.ContainsKey(hash))
                return _rpcReflector.serverMethods[hash].Invoke(this, sender, args);
            return null;
        }

        internal static void RPCCallHandler(NetworkChannel channel, RPCPacket packet, ulong sender, bool isServer) {
            if (isServer) {
                if (SpawnManager.spawnedObjects.ContainsKey(packet.networkID)) {
                    var identity = SpawnManager.spawnedObjects[packet.networkID];
                    var behaviour = identity.behaviours[packet.behaviourOrder];
                    if (behaviour._rpcReflector.serverMethods.ContainsKey(packet.methodHash)) {
                        using (var paramReader = NetworkReaderPool.GetReader(packet.parameterBuffer)) {
                            var result = behaviour.InvokeServerRPCLocal(packet.methodHash, sender, paramReader);

                            if (packet.expectsResponse) {
                                var responsePacket = new RPCResponsePacket {
                                                                               result = result,
                                                                               responseID = packet.responseID
                                                                           };
                                channel.ServerSend(sender, responsePacket);
                            }
                        }
                    }
                }
            } else {
                if (SpawnManager.spawnedObjects.ContainsKey(packet.networkID)) {
                    var identity = SpawnManager.spawnedObjects[packet.networkID];
                    var behaviour = identity.behaviours[packet.behaviourOrder];
                    if (behaviour._rpcReflector.clientMethods.ContainsKey(packet.methodHash))
                        using (var paramReader = NetworkReaderPool.GetReader(packet.parameterBuffer)) {
                            var result = behaviour.InvokeClientRPCLocal(packet.methodHash, paramReader);

                            if (packet.expectsResponse) {
                                var responsePacket = new RPCResponsePacket {
                                                                               result = result,
                                                                               responseID = packet.responseID
                                                                           };
                                channel.ClientSend(responsePacket);
                            }
                        }
                }
            }
        }

        private static StringBuilder _hashBuilder = new StringBuilder();

        private static string GetHashableMethodSignature(MethodInfo info) {
            _hashBuilder.Length = 0;
            _hashBuilder.Append(info.Name);
            foreach (var param in info.GetParameters())
                _hashBuilder.Append(param.ParameterType.Name);
            return _hashBuilder.ToString();
        }
        
        internal static ulong HashMethodName(string name) {
            switch (NetworkManager.instance.networkConfig.rpcHashSize) {
                case HashSize.TwoBytes:
                    return name.GetStableHash16();
                case HashSize.FourBytes:
                    return name.GetStableHash32();
                case HashSize.EightBytes:
                    return name.GetStableHash64();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal static ulong HashMethodSignature(MethodInfo info) {
            return HashMethodName(GetHashableMethodSignature(info));
        }

        #endregion
        
        #region Replicated Vars

        internal static readonly int VAR_DELTA_ID = 128; // Some arbitrary large number ¯\_(ツ)_/¯
        
        private VarReflector _varReflector;

        private void UpdateVars() {
            // TODO: This will be called each variable poll time.

            if (_varReflector.AnyDirty(isServer, NetworkManager.instance.localClientID)) {
                using (var writer = NetworkWriterPool.GetWriter()) {
                    // Write network id and behaviour index
                    writer.WritePackedUInt64(networkID);
                    writer.WritePackedInt32(behaviourIndex);

                    // Write all variable deltas
                    _varReflector.WriteDeltas(writer);
                    
                    // Send to all observing clients.
                    if (isServer)
                        NetworkChannel.Reliable.ServerSend(identity.observers, VAR_DELTA_ID, writer);
                    else throw new NotImplementedException();
                }
            }
        }

        internal static void HandleVarDelta(NetworkChannel channel, NetworkReader reader, ulong sender, bool isServer) {
            var networkID = reader.ReadPackedUInt64();
            var behaviourIndex = reader.ReadPackedInt32();
            
            // TODO: Server security checks!

            if (SpawnManager.spawnedObjects.TryGetValue(networkID, out var identity)) {
                if (behaviourIndex < identity.behaviours.Count) {
                    var behaviour = identity.behaviours[behaviourIndex];
                    behaviour._varReflector.ReadDeltas(reader);
                }
            }
        }

        #endregion
    }
}