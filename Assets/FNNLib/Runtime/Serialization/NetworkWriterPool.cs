using System;

namespace FNNLib.Serialization {
    public class PooledNetworkWriter : NetworkWriter, IDisposable {
        public void Dispose() {
            NetworkWriterPool.Recycle(this);
        }
    }
    
    public static class NetworkWriterPool {
        /// <summary>
        /// The capacity of the pool.
        /// </summary>
        public static int capacity {
            get => _pool.Length;
            set {
                // Resize the pool
                Array.Resize(ref _pool, value);
                
                // Adjust next if the capacity is smaller
                _nextWriter = Math.Min(_nextWriter, _pool.Length - 1);
            }
        }

        /// <summary>
        /// The network writer pool
        /// </summary>
        private static PooledNetworkWriter[] _pool = new PooledNetworkWriter[100];

        /// <summary>
        /// The index of the next available pooled writer.
        /// -1 if there is nothing in the pool.
        /// </summary>
        private static int _nextWriter = -1;

        /// <summary>
        /// Get a pooled NetworkWriter.
        /// </summary>
        /// <returns></returns>
        public static PooledNetworkWriter GetWriter() {
            if (_nextWriter == -1) {
                return new PooledNetworkWriter();
            }

            var writer = _pool[_nextWriter];
            _pool[_nextWriter] = null;
            _nextWriter--;
            
            // Reset writer for use
            writer.Reset();
            return writer;
        }

        public static void Recycle(PooledNetworkWriter writer) {
            // Add to pool if there is space
            if (_nextWriter < _pool.Length) {
                _nextWriter++;
                _pool[_nextWriter] = writer;
            }
            // If no space, leave for garbage collection
        }
    }
}