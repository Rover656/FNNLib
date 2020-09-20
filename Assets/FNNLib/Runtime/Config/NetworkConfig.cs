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
        public ushort protocolVersion;

        /// <summary>
        /// The transport to use.
        /// </summary>
        public Transport transport;

        /// <summary>
        /// The maximum age of a buffered packet before it is ignored.
        /// Default: 2 seconds.
        ///
        /// TODO: Actually implement freeing of old packets.
        /// </summary>
        public float maxBufferedPacketAge = 10f;
        
        #endregion
        
        #region Scene Manager

        /// <summary>
        /// The initial starting scene.
        /// If left blank, it will use the scene when StartServer/StartHost is called.
        /// </summary>
        [Tooltip("The initial starting scene. IF left blank, it will use the active scene at the time the server or host is started.")]
        public string initialScene;

        /// <summary>
        /// The list of scenes that the server is permitted to send the client to.
        /// Used to prevent rogue servers sending clients to invalid scenes.
        /// </summary>
        [Tooltip("The list of scenes that the server may send the client to. Used to prevent rogue servers sending clients to invalid scenes.")]
        public List<NetworkableScene> networkableScenes = new List<NetworkableScene>();
        
        #endregion
        
        #region Spawn Manager

        /// <summary>
        /// List of all networked prefabs.
        /// </summary>
        [Tooltip("List of every networked prefab. All of which must have unique hashes.")]
        public List<NetworkPrefab> networkedPrefabs = new List<NetworkPrefab>();
        
        #endregion
        
        #region Client Specific
        
        #endregion
        
        #region Server Specific
        
        /// <summary>
        /// (Dedicated) server update frequency.
        /// </summary>
        [Tooltip("Dedicated server update frequency. Prevents high CPU usage from an unlocked tickrate.")]
        public int serverTickRate = 30;

        /// <summary>
        /// The number of seconds to wait for a client to request connection before dropping them.
        /// </summary>
        public int connectionRequestTimeout = 15;

        /// <summary>
        /// The number of seconds to wait for a client to acknowledge a disconnection request before dropping them.
        /// </summary>
        public int disconnectRequestTimeout = 15;
        
        #endregion
        
        #region Hashing

        /// <summary>
        /// The hash size used for packet IDs.
        /// Only change if you are having collision problems.
        /// </summary>
        public HashSize packetIDHashSize = HashSize.FourBytes;
        
        /// <summary>
        /// The hash size for rpc method names.
        /// Only change if you are having collision problems.
        /// TODO: Implement
        /// </summary>
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