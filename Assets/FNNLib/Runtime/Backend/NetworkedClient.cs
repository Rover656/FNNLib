using System.Collections.Generic;

namespace FNNLib {
    public class NetworkedClient {
        public ulong clientID;

        public List<uint> loadedScenes = new List<uint>();

        public ulong playerObject;

        public readonly List<ulong> ownedObjects = new List<ulong>();
    }
}