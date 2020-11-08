using System.Collections.Generic;
using FNNLib.SceneManagement;
using FNNLib.Spawning;

namespace FNNLib {
    public class NetworkClient {
        public ulong ID { get; internal set; }

        public readonly List<NetworkScene> loadedScenes = new List<NetworkScene>();

        public ulong playerObject;

        public readonly List<ulong> ownedObjects = new List<ulong>();
    }
}