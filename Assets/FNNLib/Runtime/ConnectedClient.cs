using System.Collections.Generic;

namespace FNNLib {
    public class ConnectedClient {
        public ulong clientID;

        public NetworkIdentity playerObject;

        public readonly List<NetworkIdentity> ownedObjects = new List<NetworkIdentity>();
    }
}