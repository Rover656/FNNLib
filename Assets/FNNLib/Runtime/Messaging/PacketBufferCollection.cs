using System.Collections.Generic;

namespace FNNLib.Messaging {
    /// <summary>
    /// A collection of keyed buffers.
    /// This is a helper class to try and help prevent repeated code.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    public class PacketBufferCollection<TKey> : BasePacketBufferCollection {
        /// <summary>
        /// All of the buffers in the collection
        /// </summary>
        private Dictionary<TKey, PacketBuffer> _buffers = new Dictionary<TKey, PacketBuffer>();

        /// <summary>
        /// Enqueue a buffered packet with a key.
        /// </summary>
        /// <param name="key">The key to buffer with.</param>
        /// <param name="packet">The packet to buffer.</param>
        public void Enqueue(TKey key, BufferedPacket packet) {
            if (!_buffers.ContainsKey(key))
                _buffers.Add(key, new PacketBuffer());
            _buffers[key].Enqueue(packet);
        }

        /// <summary>
        /// Execute the packet buffer stored at key.
        /// </summary>
        /// <param name="key">The key.</param>
        public void ExecutePending(TKey key) {
            if (!_buffers.ContainsKey(key))
                return;
            _buffers[key].ExecutePending();

            if (_buffers[key].count == 0)
                _buffers.Remove(key);
        }

        /// <summary>
        /// Whether or not the buffer at key has pending packets.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>Whether or not the buffer has pending packets.</returns>
        public bool HasPending(TKey key) {
            if (!_buffers.ContainsKey(key))
                return false;
            return _buffers[key].count > 0;
        }

        /// <summary>
        /// Purge all old packets across all buffers.
        /// </summary>
        public override void PurgeOldPackets() {
            var remove = new List<TKey>();
            foreach (var buffer in _buffers) {
                buffer.Value.PurgeOldPackets();
                if (buffer.Value.count == 0)
                    remove.Add(buffer.Key);
            }

            foreach (var key in remove)
                _buffers.Remove(key);
        }

        /// <summary>
        /// Destroy the given queue.
        /// </summary>
        /// <param name="key">The key.</param>
        public void DestroyQueue(TKey key) {
            if (_buffers.ContainsKey(key))
                _buffers.Remove(key);
        }
    }
}