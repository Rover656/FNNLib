using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FNNLib.Config;
using FNNLib.Messaging;
using FNNLib.RPC;
using FNNLib.SceneManagement;
using FNNLib.Serialization;
using FNNLib.Spawning;
using FNNLib.Utilities;
using UnityEngine;

namespace FNNLib {
    /// <summary>
    /// A behaviour that is network aware.
    /// </summary>
    public abstract partial class NetworkBehaviour : MonoBehaviour {
        #region Identity Fetch

        /// <summary>
        /// The cached reference to the network identity.
        /// </summary>
        private NetworkIdentity _identity;

        /// <summary>
        /// The identity the behaviour functions under.
        /// </summary>
        public NetworkIdentity identity {
            get {
                if (_identity == null)
                    _identity = GetComponentInParent<NetworkIdentity>();
                return _identity;
            }
        }

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

        /// <summary>
        /// Is running in a host context?
        /// </summary>
        public bool isHost => NetworkManager.instance.isHost;

        #region Behaviour Identification

        /// <summary>
        /// This behaviour's index within the identity.
        /// Using this with the identity network ID provides a unique identifier to this behaviour anywhere on the network.
        /// </summary>
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
            _rpcReflectionData = RPCReflectionData.GetOrCreate(GetType());
            rpcDelegates = _rpcReflectionData.CreateTargetDelegates(this);
        }

        #endregion

        #region RPCs

        /// <summary>
        /// RPC method reflection data.
        /// </summary>
        private RPCReflectionData _rpcReflectionData;
        
        /// <summary>
        /// List of RPC delegates for this behaviour.
        /// </summary>
        internal RPCDelegate[] rpcDelegates;

        private void SendClientRPCCall(ulong hash, List<ulong> clients, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                if (isHost && identity.observers.Contains(NetworkManager.instance.localClientID) &&
                    clients.Contains(NetworkManager.instance.localClientID))
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment()))
                        InvokeRPCLocal(hash, NetworkManager.ServerLocalID, reader, false);

                var packet = new RPCPacket {
                                               behaviourIndex = behaviourIndex,
                                               methodHash = hash,
                                               networkID = networkID,
                                               parameterBuffer = writer.ToArraySegment()
                                           };
                NetworkChannel.ReliableSequenced.ServerSend(clients, packet);
            }
        }

        private void SendClientRPCCallOn(ulong hash, ulong client, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                // If we're the host, ignore sending the packet.
                if (isHost && client == NetworkManager.instance.localClientID)
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeRPCLocal(hash, NetworkManager.ServerLocalID, reader, false);
                    }
                else {
                    var packet = new RPCPacket {
                                                   behaviourIndex = behaviourIndex,
                                                   methodHash = hash,
                                                   networkID = networkID,
                                                   parameterBuffer = writer.ToArraySegment()
                                               };
                    NetworkChannel.ReliableSequenced.ServerSend(client, packet);
                }
            }
        }
        
        private RPCResponse<T> SendClientRPCCallOnResponse<T>(ulong hash, ulong client, params object[] args) {
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
                        var result = InvokeRPCLocal(hash, NetworkManager.ServerLocalID, reader, false);
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
                                               behaviourIndex = behaviourIndex,
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

        private void SendClientRPCCallAll(ulong hash, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                if (isHost && identity.observers.Contains(NetworkManager.instance.localClientID))
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeRPCLocal(hash, NetworkManager.ServerLocalID, reader, false);
                    }

                var packet = new RPCPacket {
                                               behaviourIndex = behaviourIndex,
                                               methodHash = hash,
                                               networkID = networkID,
                                               parameterBuffer = writer.ToArraySegment()
                                           };
                NetworkChannel.ReliableSequenced.ServerSend(identity.observers, packet);
            }
        }

        private void SendClientRPCCallAllExcept(ulong hash, ulong excludedClient, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                if (isHost && identity.observers.Contains(NetworkManager.instance.localClientID))
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeRPCLocal(hash, NetworkManager.ServerLocalID, reader, false);
                    }

                var packet = new RPCPacket {
                                               behaviourIndex = behaviourIndex,
                                               methodHash = hash,
                                               networkID = networkID,
                                               parameterBuffer = writer.ToArraySegment()
                                           };
                NetworkChannel.ReliableSequenced.ServerSend(identity.observers, packet, excludedClient);
            }
        }

        private void SendServerRPCCall(ulong hash, params object[] args) {
            // Block non-client calls
            if (!isClient)
                throw new NotSupportedException("Only the client may invoke RPCs on the server.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                if (isHost)
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeRPCLocal(hash, NetworkManager.instance.localClientID, reader, true);
                    }
                else {
                    var packet = new RPCPacket {
                                                   behaviourIndex = behaviourIndex,
                                                   methodHash = hash,
                                                   networkID = networkID,
                                                   parameterBuffer = writer.ToArraySegment()
                                               };
                    NetworkChannel.ReliableSequenced.ClientSend(packet);
                }
            }
        }
        
        private RPCResponse<T> SendServerRPCCallResponse<T>(ulong hash, params object[] args) {
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
                        var result = InvokeRPCLocal(hash, NetworkManager.instance.localClientID, reader, true);
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
                                               behaviourIndex = behaviourIndex,
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

        /// <summary>
        /// Invoke a local RPC method.
        /// </summary>
        /// <param name="hash">The method hash.</param>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments in the form of a reader.</param>
        /// <param name="isServer">Whether or not we are the server.</param>
        /// <returns>The return value of the method or null.</returns>
        private object InvokeRPCLocal(ulong hash, ulong sender, NetworkReader args, bool isServer) {
            if (isServer) {
                if (_rpcReflectionData.serverMethods.ContainsKey(hash))
                    return _rpcReflectionData.serverMethods[hash].Invoke(this, sender, args);

                return null;
            }

            if (_rpcReflectionData.clientMethods.ContainsKey(hash))
                return _rpcReflectionData.clientMethods[hash].Invoke(this, sender, args);

            return null;
        }

        /// <summary>
        /// Handle an RPC call packet.
        /// </summary>
        /// <param name="channel">Channel the packet was received on.</param>
        /// <param name="packet">The packet received.</param>
        /// <param name="sender">The packet sender.</param>
        /// <param name="isServer">Whether or not we are the server.</param>
        internal static void RPCCallHandler(NetworkChannel channel, RPCPacket packet, ulong sender, bool isServer) {
            // Find the object
            if (SpawnManager.spawnedObjects.ContainsKey(packet.networkID)) {
                // Get the identity and locate the behaviour
                var identity = SpawnManager.spawnedObjects[packet.networkID];

                // Ensure behaviour index is in range
                if (packet.behaviourIndex < identity.behaviours.Count) {
                    // Get the behaviour and try to find the method
                    var behaviour = identity.behaviours[packet.behaviourIndex];
                    
                    bool hasMethod;
                    if (isServer) {
                        hasMethod = behaviour._rpcReflectionData.serverMethods.ContainsKey(packet.methodHash);
                    } else {
                        hasMethod = behaviour._rpcReflectionData.clientMethods.ContainsKey(packet.methodHash);
                    }
                    
                    // If we have the method, call it
                    if (hasMethod) {
                        using (var paramReader = NetworkReaderPool.GetReader(packet.parameterBuffer)) {
                            // Invoke the RPC method
                            var result = behaviour.InvokeRPCLocal(packet.methodHash, sender, paramReader, isServer);

                            if (packet.expectsResponse) {
                                // Build the RPC response.
                                var responsePacket = new RPCResponsePacket {
                                                                               result = result,
                                                                               responseID = packet.responseID
                                                                           };

                                // Send the response
                                if (isServer) {
                                    channel.ServerSend(sender, responsePacket);
                                } else {
                                    channel.ClientSend(responsePacket);
                                }
                            }
                        }
                    } else {
                        Debug.LogWarning("Attempted RPC call to non-existing method.");
                    }
                } else {
                    Debug.LogWarning("Attempted RPC call to non-existing behaviour.");
                }
            } else {
                Debug.LogWarning("Attempted RPC call to non-existing identity.");
            }
        }

        /// <summary>
        /// String builder used for creating method signature strings.
        /// </summary>
        private static StringBuilder _hashBuilder = new StringBuilder();

        /// <summary>
        /// Convert a method signature to a string for hashing.
        /// </summary>
        /// <param name="info">The method signature.</param>
        /// <returns>A hashable string.</returns>
        private static string GetHashableMethodSignature(MethodInfo info) {
            _hashBuilder.Length = 0;
            _hashBuilder.Append(info.Name);
            foreach (var param in info.GetParameters())
                _hashBuilder.Append(param.ParameterType.Name);
            return _hashBuilder.ToString();
        }
        
        /// <summary>
        /// Hash a method name.
        /// Used for identifying a method (not guaranteed to be unique).
        /// </summary>
        /// <param name="name">Name of the method</param>
        /// <returns>The hash.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
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

        /// <summary>
        /// Hash a method signature.
        /// Used for uniquely identifying a method.
        /// </summary>
        /// <param name="info">The method signature to be hashed.</param>
        /// <returns>The hash.</returns>
        internal static ulong HashMethodSignature(MethodInfo info) {
            return HashMethodName(GetHashableMethodSignature(info));
        }

        #endregion
    }
}