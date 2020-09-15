using System.Collections.Generic;

namespace FNNLib {
    public class ConnectedClient {
        public ulong clientID;

        public uint sceneID;

        public ulong playerObject;

        public readonly List<ulong> ownedObjects = new List<ulong>();
    }
}