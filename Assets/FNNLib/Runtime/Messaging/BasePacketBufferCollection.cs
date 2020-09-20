using System.Collections.Generic;

namespace FNNLib.Messaging {
    /// <summary>
    /// The base class for the packet buffer collection.
    /// Allows us to process every existing buffer collection at once and empty old packets.
    /// </summary>
    public abstract class BasePacketBufferCollection {
        private static List<BasePacketBufferCollection> _allCollections = new List<BasePacketBufferCollection>();

        protected BasePacketBufferCollection() {
            _allCollections.Add(this);
        }

        ~BasePacketBufferCollection() {
            _allCollections.Remove(this);
        }

        /// <summary>
        /// Purge the old packets on every existing collection
        /// </summary>
        internal static void PurgeAllOldPackets() {
            foreach (var collection in _allCollections) {
                collection.PurgeOldPackets();
            }
        }
        
        public abstract void PurgeOldPackets();
    }
}