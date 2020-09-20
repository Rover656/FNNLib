using System.Collections.Generic;

namespace FNNLib.Messaging {
    public class PacketBufferCollection<TKey> {
        private Dictionary<TKey, PacketBuffer> _buffers = new Dictionary<TKey, PacketBuffer>();

        public void Enqueue(TKey key, BufferedPacket packet) {
            if (!_buffers.ContainsKey(key))
                _buffers.Add(key, new PacketBuffer());
            _buffers[key].Enqueue(packet);
        }

        public void ExecutePending(TKey key) {
            if (!_buffers.ContainsKey(key))
                return;
            _buffers[key].ExecutePending();

            if (_buffers[key].count == 0)
                _buffers.Remove(key);
        }

        public bool HasPending(TKey key) {
            if (!_buffers.ContainsKey(key))
                return false;
            return _buffers[key].count > 0;
        }

        public void PurgeOldPackets() {
            var remove = new List<TKey>();
            foreach (var buffer in _buffers) {
                buffer.Value.PurgeOldPackets();
                if (buffer.Value.count == 0)
                    remove.Add(buffer.Key);
            }

            foreach (var key in remove)
                _buffers.Remove(key);
        }

        public void DestroyQueue(TKey key) {
            if (_buffers.ContainsKey(key))
                _buffers.Remove(key);
        }
    }
}