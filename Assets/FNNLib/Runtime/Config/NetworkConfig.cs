using System;
using System.Collections.Generic;
using FNNLib.SceneManagement;
using FNNLib.Serialization;
using FNNLib.Transports;
using FNNLib.Utilities;
using UnityEngine;

namespace FNNLib.Config {
    [Serializable]
    public class NetworkConfig {
        #region General Config
        
        /// <summary>
        /// The protocol version.
        /// This prevents cross-version communication.
        /// You can however keep the protocol version the same if a minor bugfix or change is done that doesn't affect gameplay.
        /// </summary>
        [Tooltip("The protocol version. This prevents cross-version communication.")]
        public ushort protocolVersion;

        /// <summary>
        /// The transport to use for networking.
        /// </summary>
        [Tooltip("The transport to use for networking.")]
        public Transport transport;

        /// <summary>
        /// The maximum age of a buffered packet before it is ignored.
        /// </summary>
        [Tooltip("The maximum age of a buffered packet before it is ignored.")]
        public float maxBufferedPacketAge = 10f;

        /// <summary>
        /// The number of buffer purges to perform per second.
        /// TODO: Add to editor
        /// </summary>
        public int bufferPurgesPerSecond = 3;
        
        #endregion
        
        #region Scene Manager

        /// <summary>
        /// The initial starting scene.
        /// If left blank, it will use the scene when StartServer/StartHost is called.
        /// </summary>
        [Tooltip("The initial starting scene. If left blank, it will use the active scene at the time the server or host is started.")]
        public string initialScene;

        /// <summary>
        /// The list of scenes that the server is permitted to send the client to.
        /// Used to prevent rogue servers sending clients to invalid scenes.
        /// </summary>
        [Tooltip("The list of scenes that the server may send the client to. Used to prevent rogue servers sending clients to invalid scenes.")]
        public List<NetworkableScene> networkableScenes = new List<NetworkableScene>();

        /// <summary>
        /// Whether or not hybrid scenes are enabled.
        /// This will enable the use of scenes that are additively loaded (and packed) on the server and single loaded on clients.
        /// </summary>
        [Tooltip("Whether or not hybrid scenes are enabled. This will enable the use of scenes that are additively loaded (and packed) on the server and single loaded on clients.")]
        public bool enableHybridScenes;
        
        #endregion
        
        #region Spawn Manager

        /// <summary>
        /// List of all networked prefabs.
        /// </summary>
        [Tooltip("List of every networked prefab. All of which must have unique hashes.")]
        public List<NetworkPrefab> networkedPrefabs = new List<NetworkPrefab>();
        
        #endregion
        
        #region Client Specific
        
        /// <summary>
        /// Maximum number of updates to process per tick as the client.
        /// </summary>
        [Tooltip("Maximum number of updates to process per tick as the client.")]
        public int clientMaxReceivesPerUpdate = 1000;
        
        #endregion
        
        #region Server Specific
        
        /// <summary>
        /// (Dedicated) server update frequency.
        /// </summary>
        [Tooltip("Dedicated server update frequency. Prevents high CPU usage from an unlocked tickrate.")]
        public int serverTickRate = 30;
        
        /// <summary>
        /// Maximum number of updates to process per tick as the server.
        /// </summary>
        [Tooltip("Maximum number of updates to process per tick as the server.")]
        public int serverMaxReceivesPerUpdate = 10000;

        /// <summary>
        /// The number of seconds to wait for a client to request connection before dropping them.
        /// </summary>
        [Tooltip("The number of seconds to wait for a client to request connection before dropping them.")]
        public int connectionRequestTimeout = 15;

        /// <summary>
        /// The number of seconds to wait for a client to acknowledge a disconnection request before dropping them.
        /// </summary>
        [Tooltip("The number of seconds to wait for a client to acknowledge a disconnection request before dropping them.")]
        public int disconnectRequestTimeout = 15;
        
        #endregion
        
        #region Hashing

        /// <summary>
        /// The hash size used for packet IDs.
        /// Only change if you are having collision problems.
        /// </summary>
        [Tooltip("The hash size used for packet IDs. Only change if you are having collision problems.")]
        public HashSize packetIDHashSize = HashSize.FourBytes;
        
        /// <summary>
        /// The hash size for RPC method names/signatures.
        /// Only change if you are having collision problems.
        /// </summary>
        [Tooltip("The hash size for RPC method names/signatures. Only change if you are having collision problems.")]
        public HashSize rpcHashSize = HashSize.FourBytes;

        #endregion
        
        #region Config Comparisons

        /// <summary>
        /// FNNLib protocol version.
        /// Used to ensure that both client and server are utilising the same FNNLib protocol.
        /// If the are not, serious problems could occur.
        /// This is only incremented if a protocol change is made within FNNLib that will cause incompatibilities.
        /// </summary>
        internal const ushort FNNLIB_PROTOCOL_VER = 0;

        /// <summary>
        /// Cached hash. Nothing should really be changed at runtime anyway.
        /// </summary>
        private ulong? _cachedHash;

        /// <summary>
        /// Get the config hash.
        /// This is used to compare the validity of a connection.
        /// </summary>
        /// <param name="cache"></param>
        /// <returns></returns>
        public ulong GetHash(bool cache = true) {
            if (_cachedHash != null && cache)
                return _cachedHash.Value;

            using (var writer = NetworkWriterPool.GetWriter()) {
                // Write user-defined protocol version
                writer.WritePackedUInt16(protocolVersion);
                
                // Write FNNLib protocol version
                writer.WritePackedUInt16(FNNLIB_PROTOCOL_VER);

                // Write config that must be the same across client and server.
                writer.WriteByte((byte) packetIDHashSize);
                writer.WriteByte((byte) rpcHashSize);

                if (cache) {
                    _cachedHash = writer.ToArray().GetStableHash64();
                    return _cachedHash.Value;
                }

                return writer.ToArray().GetStableHash64();
            }
        }

        #endregion
    }
}