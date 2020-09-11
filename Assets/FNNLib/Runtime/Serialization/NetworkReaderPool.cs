using System;

namespace FNNLib.Serialization {
    public class PooledNetworkReader : NetworkReader, IDisposable {
        internal PooledNetworkReader(byte[] bytes) : base(bytes) { }
        internal PooledNetworkReader(ArraySegment<byte> segment) : base(segment) { }

        public void Dispose() {
            NetworkReaderPool.Recycle(this);
        }
    }
    
    public static class NetworkReaderPool {
        public static int capacity {
            get => _pool.Length;
            set {
                // Resize pool
                Array.Resize(ref _pool, value);
                
                // Adjust next if the capacity is smaller
                _nextReader = Math.Min(_nextReader, _pool.Length - 1);
            }
        }
        
        private static PooledNetworkReader[] _pool = new PooledNetworkReader[100];

        private static int _nextReader = -1;

        public static PooledNetworkReader GetReader(byte[] bytes) {
            if (_nextReader == -1) {
                return new PooledNetworkReader(bytes);
            }

            var reader = _pool[_nextReader];
            _pool[_nextReader] = null;
            _nextReader--;
            
            // Set buffer
            SetBuffer(reader, bytes);
            return reader;
        }
        
        public static PooledNetworkReader GetReader(ArraySegment<byte> segment) {
            if (_nextReader == -1) {
                return new PooledNetworkReader(segment);
            }

            var reader = _pool[_nextReader];
            _pool[_nextReader] = null;
            _nextReader--;
            
            // Set buffer
            SetBuffer(reader, segment);
            return reader;
        }

        public static void Recycle(PooledNetworkReader reader) {
            
        }

        private static void SetBuffer(NetworkReader reader, byte[] bytes) {
            reader.buffer = new ArraySegment<byte>(bytes);
            reader.position = 0;
        }
        
        private static void SetBuffer(NetworkReader reader, ArraySegment<byte> segment) {
            reader.buffer = segment;
            reader.position = 0;
        }
    }
}