using System.Collections.Generic;
using FNNLib.SceneManagement;
using FNNLib.Spawning;

namespace FNNLib {
    public class NetworkClient {
        /// <summary>
        /// The Network Client ID.
        /// This should be used when dealing with the client on a lower network level.
        /// Otherwise pass the NetworkClient class.
        /// </summary>
        public ulong clientID { get; internal set; }

        /// <summary>
        /// List of scenes that this client has loaded.
        /// </summary>
        public readonly List<NetworkScene> loadedScenes = new List<NetworkScene>();

        public ulong playerObject;

        public readonly List<ulong> ownedObjects = new List<ulong>();

        public bool isConnected => NetworkManager.instance != null &&
                                   NetworkManager.instance.connectedClients.ContainsKey(clientID);
    }
}