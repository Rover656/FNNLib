using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using FNNLib.Backend;
using FNNLib.RPC;
using FNNLib.SceneManagement;
using FNNLib.Serialization;
using FNNLib.Spawning;
using FNNLib.Transports;
using FNNLib.Utilities;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FNNLib {
    // TODO: This will be fleshed out once object spawning and scene management is done.
    [RequireComponent(typeof(NetworkIdentity))]
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

        public bool hasIdentity => identity != null

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
            _rpcReflectionData = RPCReflectionData.GetOrCreate(GetType());
            rpcDelegates = _rpcReflectionData.CreateTargetDelegates(this);
        }

        #endregion

        #region RPCs

        private RPCReflectionData _rpcReflectionData;
        internal RPCDelegate[] rpcDelegates;

        internal void SendClientRPCCall(ulong hash, List<ulong> clients, int channel, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(arg);

                if (isHost && identity.observers.Contains(NetworkManager.instance.localClientID) &&
                    clients.Contains(NetworkManager.instance.localClientID))
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeClientRPCLocal(hash, NetworkManager.instance.localClientID, reader);
                    }

                var packet = new RPCPacket {
                                               behaviourOrder = behaviourIndex,
                                               methodHash = hash,
                                               networkID = networkID,
                                               parameterBuffer = writer.ToArraySegment()
                                           };
                NetworkServer.instance.Send(clients, packet, channel);
            }
        }

        internal void SendClientRPCCallFor(ulong hash, ulong client, int channel, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                // If we're the host, ignore sending the packet.
                if (isHost && client == NetworkManager.instance.localClientID)
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeClientRPCLocal(hash, NetworkManager.instance.localClientID, reader);
                    }
                else {
                    var packet = new RPCPacket {
                                                   behaviourOrder = behaviourIndex,
                                                   methodHash = hash,
                                                   networkID = networkID,
                                                   parameterBuffer = writer.ToArraySegment()
                                               };
                    NetworkServer.instance.Send(client, packet, channel);
                }
            }
        }

        internal void SendClientRPCCallAll(ulong hash, int channel, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                if (isHost && identity.observers.Contains(NetworkManager.instance.localClientID))
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeClientRPCLocal(hash, NetworkManager.instance.localClientID, reader);
                    }

                var packet = new RPCPacket {
                                               behaviourOrder = behaviourIndex,
                                               methodHash = hash,
                                               networkID = networkID,
                                               parameterBuffer = writer.ToArraySegment()
                                           };
                NetworkServer.instance.Send(identity.observers, packet, channel);
            }
        }
        
        internal void SendClientRPCCallAllExcept(ulong hash, ulong excludedClient, int channel, params object[] args) {
            // Block non-server calls
            if (!isServer)
                throw new NotSupportedException("Only the server may invoke RPCs on clients.");

            // Write parameters
            using (var writer = NetworkWriterPool.GetWriter()) {
                writer.WritePackedObjects(args);

                if (isHost && identity.observers.Contains(NetworkManager.instance.localClientID))
                    using (var reader = NetworkReaderPool.GetReader(writer.ToArraySegment())) {
                        InvokeClientRPCLocal(hash, NetworkManager.instance.localClientID, reader);
                    }

                var packet = new RPCPacket {
                                               behaviourOrder = behaviourIndex,
                                               methodHash = hash,
                                               networkID = networkID,
                                               parameterBuffer = writer.ToArraySegment()
                                           };
                NetworkServer.instance.SendExcluding(identity.observers, excludedClient, packet, channel);
            }
        }

        internal void SendServerRPCCall(ulong hash, int channel, params object[] args) {
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
                    NetworkClient.instance.Send(packet, channel);
                }
            }
        }

        private object InvokeClientRPCLocal(ulong hash, ulong sender, NetworkReader args) {
            if (_rpcReflectionData.clientMethods.ContainsKey(hash))
                _rpcReflectionData.clientMethods[hash].Invoke(this, sender, args);

            return null;
        }
        
        private object InvokeServerRPCLocal(ulong hash, ulong sender, NetworkReader args) {
            if (_rpcReflectionData.serverMethods.ContainsKey(hash))
                _rpcReflectionData.serverMethods[hash].Invoke(this, sender, args);

            return null;
        }

        internal static void ClientRPCCallHandler(ulong sender, RPCPacket packet) {
            if (SpawnManager.spawnedObjects.ContainsKey(packet.networkID)) {
                var identity = SpawnManager.spawnedObjects[packet.networkID];
                var behaviour = identity.behaviours[packet.behaviourOrder];
                if (behaviour._rpcReflectionData.clientMethods.ContainsKey(packet.methodHash))
                    using (var paramReader = NetworkReaderPool.GetReader(packet.parameterBuffer)) {
                        behaviour.InvokeClientRPCLocal(packet.methodHash, sender, paramReader);
                    }
            }
        }
        
        internal static void ServerRPCCallHandler(ulong sender, RPCPacket packet) {
            if (SpawnManager.spawnedObjects.ContainsKey(packet.networkID)) {
                var identity = SpawnManager.spawnedObjects[packet.networkID];
                var behaviour = identity.behaviours[packet.behaviourOrder];
                if (behaviour._rpcReflectionData.serverMethods.ContainsKey(packet.methodHash))
                    using (var paramReader = NetworkReaderPool.GetReader(packet.parameterBuffer)) {
                        behaviour.InvokeServerRPCLocal(packet.methodHash, sender, paramReader);
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
            // TODO: Allow changing hash size
            return name.GetStableHash64();
        }

        internal static ulong HashMethodSignature(MethodInfo info) {
            return HashMethodName(GetHashableMethodSignature(info));
        }

        #endregion
    }
}