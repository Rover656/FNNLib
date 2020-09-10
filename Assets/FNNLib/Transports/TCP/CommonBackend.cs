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

using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace FNNLib.Transports.TCP {
    public class CommonBackend {
        public static int MaxMessageSize = 16 * 1024;
        
        protected ConcurrentQueue<Message> recieveQueue = new ConcurrentQueue<Message>();
        
        // TODO: Warnings about queue length etc.

        public bool TryGetMessage(out Message message) {
            return recieveQueue.TryDequeue(out message);
        }
        
        [ThreadStatic]
        private static byte[] header;
        
        [ThreadStatic]
        private static byte[] payload;
        
        protected static bool SendDataBlocking(NetworkStream stream, byte[][] data) {
            try {
                // Combine multiple pending data packets into one packet for performance sakes
                var packetSize = 0;
                for (var i = 0; i < data.Length; i++) {
                    packetSize += sizeof(int) + data[i].Length; // header + content
                }

                // Create payload if it doesn't exist or is too small
                if (payload == null || payload.Length < packetSize)
                    payload = new byte[packetSize];
                
                // Create the packet
                var position = 0;
                for (var i = 0; i < data.Length; i++) {
                    if (header == null)
                        header = new byte[4];
                    
                    // Build header
                    Utils.IntToBytesBigEndianNonAlloc(data[i].Length, header);
                    
                    // Copy header and message into payload
                    Array.Copy(header, 0, payload, position, header.Length);
                    Array.Copy(data[i], 0, payload, position + header.Length, data[i].Length);
                    position += header.Length + data[i].Length;
                }
                
                // Write payload to the stream
                stream.Write(payload, 0, packetSize);
                return true;
            }
            catch (Exception ex) {
                Debug.Log("SendDataBlocking: " + ex);
                return false;
            }
        }

        protected static bool ReadDataBlocking(NetworkStream stream, int maxMessageSize, out byte[] content) {
            // Set content to null
            content = null;
            
            // Create header if not created yet
            if (header == null)
                header = new byte[4];
            
            // Read 4 byte header
            if (!stream.ReadExactly(header, 4))
                return false;
            
            // Convert to int
            int size = Utils.BytesToIntBigEndian(header);
            
            // Protect against allocation attacks
            if (size < maxMessageSize) {
                content = new byte[size];
                return stream.ReadExactly(content, size);
            }

            Debug.LogWarning("ReadDataBlocking: possible allocation attack, header size: " + size);
            return false;
        }

        protected static void SendThread(int clientID, TcpClient client, SafeQueue<byte[]> dataQueue,
                                             ManualResetEvent dataPending) {
            // Get stream
            var stream = client.GetStream();

            try {
                while (client.Connected) {
                    // Reset, so that WaitOne blocks once we've finished sending data.
                    dataPending.Reset();

                    // Send all data in queue.
                    byte[][] allData;
                    if (dataQueue.TryDequeueAll(out allData)) {
                        if (!SendDataBlocking(stream, allData)) {
                            break;
                        }
                    }

                    // Block thread until we have more data.
                    dataPending.WaitOne();
                }
            }
            catch (ThreadAbortException) {
                // Happens when thread stops
            }
            catch (ThreadInterruptedException) {
                // Happens when thread is interrupted.
            }
            catch (Exception ex) {
                Debug.LogError("SendThread error: " + ex);
            }
            finally {
                // Clean up connections
                stream.Close();
                client.Close();
            }
        }

        protected static void ReceiveThread(int connectionID, TcpClient client, ConcurrentQueue<Message> recieveQueue,
                                          int maxMessageSize) {
            // Get stream
            var stream = client.GetStream();

            try {
                // Connected event
                recieveQueue.Enqueue(new Message(connectionID, EventType.Connect, null));

                while (true) {
                    // Read next message
                    byte[] content;
                    if (!ReadDataBlocking(stream, maxMessageSize, out content))
                        break;
                    
                    // Queue data
                    recieveQueue.Enqueue(new Message(connectionID, EventType.Data, content));
                }
            }
            catch (Exception ex) {
                Debug.LogError("RecieveLoop: " + ex);
            }
            finally {
                // Clean up connections.
                stream.Close();
                client.Close();
                
                // Disconnect message
                recieveQueue.Enqueue(new Message(connectionID, EventType.Disconnect, null));
            }
        }
    }
}