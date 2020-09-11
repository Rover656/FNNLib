// Implemented with strong reference to Mirror, so here's the license:
/*
MIT License

Copyright (c) 2015, Unity Technologies
Copyright (c) 2019, vis2k, Paul and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
 */

// Net 4.X has ConcurrentQueue, but ConcurrentQueue has no TryDequeueAll method,
// which makes SafeQueue twice as fast for the send thread.
//
// uMMORPG 450 CCU
//   SafeQueue:       900-1440ms latency
//   ConcurrentQueue:     2000ms latency
//
// It's also noticeable in the LoadTest project, which hardly handles 300 CCU
// with ConcurrentQueue!

using System.Collections.Generic;

namespace FNNLib.Transports.TCP {
    public class SafeQueue<T> {
        readonly Queue<T> queue = new Queue<T>();

        // for statistics. don't call Count and assume that it's the same after the
        // call.
        public int Count {
            get {
                lock (queue) {
                    return queue.Count;
                }
            }
        }

        public void Enqueue(T item) {
            lock (queue) {
                queue.Enqueue(item);
            }
        }

        // can't check .Count before doing Dequeue because it might change inbetween,
        // so we need a TryDequeue
        public bool TryDequeue(out T result) {
            lock (queue) {
                result = default(T);
                if (queue.Count > 0) {
                    result = queue.Dequeue();
                    return true;
                }

                return false;
            }
        }

        // for when we want to dequeue and remove all of them at once without
        // locking every single TryDequeue.
        public bool TryDequeueAll(out T[] result) {
            lock (queue) {
                result = queue.ToArray();
                queue.Clear();
                return result.Length > 0;
            }
        }

        public void Clear() {
            lock (queue) {
                queue.Clear();
            }
        }
    }
}