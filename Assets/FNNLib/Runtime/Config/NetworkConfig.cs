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
        
        #endregion
        
        #region Scene Manager
        
        /// <summary>
        /// This enables the NetworkSceneManager and will register its packets in the relevant places.
        /// TODO: Do we just force use of this?
        /// </summary>
        public bool useSceneManagement = true;

        /// <summary>
        /// Whether or not subscenes are enabled.
        /// If they are enabled, all networkable scenes must be given packing data.
        /// </summary>
        public bool enableSubScenes = false;

        /// <summary>
        /// If enabled, new clients will always be moved to the currently active scene.
        /// If this is not enabled, you will be expected to move the client yourself. I.e the client will not exist in any space until you move it.
        ///
        /// Only available if subscenes are enabled.
        /// </summary>
        public bool moveNewClientsToActiveScene = true;
        
        /// <summary>
        /// The list of scenes that the server is permitted to send the client to.
        /// Used for security purposes if you opt to use the NetworkSceneManager (recommended).
        /// </summary>
        public List<NetworkableScene> networkableScenes = new List<NetworkableScene>();
        
        #endregion
        
        #region Spawn Manager

        /// <summary>
        /// List of all networked prefabs.
        /// </summary>
        public List<NetworkPrefab> networkedPrefabs = new List<NetworkPrefab>();
        
        #endregion
        
        #region Client Specific
        
        #endregion
        
        #region Server Specific
        
        /// <summary>
        /// (Dedicated) server update frequency.
        /// </summary>
        public int serverTickRate = 30;
        
        // TODO: Use these in NetworkServer:

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
        /// The hash size used for packet IDs, scene IDs and prefab IDs.
        /// </summary>
        public HashSize hashSize = HashSize.FourBytes;

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

        public ulong GetHash(bool cache = true) {
            if (_cachedHash != null && cache)
                return _cachedHash.Value;

            using (var writer = NetworkWriterPool.GetWriter()) {
                // Write user-defined protocol version
                writer.WritePackedUInt16(protocolVersion);
                
                // Write FNNLib protocol version
                writer.WritePackedUInt16(FNNLIB_PROTOCOL_VER);

                // Write config that must be the same across client and server.
                writer.WriteBool(useSceneManagement);
                writer.WriteByte((byte) hashSize);

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