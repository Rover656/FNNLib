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
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using ThreadPriority = System.Threading.ThreadPriority;

namespace FNNLib.Transports.TCP {
    public class TCPServer : CommonBackend {
        public bool running => _listenerThread != null && _listenerThread.IsAlive;

        public TcpListener listener;

        private Thread _listenerThread;

        private class Client {
            public TcpClient client;
            public SafeQueue<byte[]> dataQueue = new SafeQueue<byte[]>();
            public ManualResetEvent dataPending = new ManualResetEvent(false);

            public Client(TcpClient client) {
                this.client = client;
            }
        }

        private readonly ConcurrentDictionary<int, Client> _clients = new ConcurrentDictionary<int, Client>();

        private int _idCounter;

        private int GetConnectionID() {
            var id = _idCounter;
            Interlocked.Increment(ref _idCounter);
            return id;
        }

        public void Start(IPAddress address, int port) {
            if (running)
                throw new NotSupportedException("Server is already running!");

            _listenerThread = new Thread(() => { ListenThread(address, port); })
                              {IsBackground = true, Priority = ThreadPriority.BelowNormal};
            _listenerThread.Start();
        }

        public void Stop() {
            if (!running)
                return;

            // Stop listener
            listener?.Stop();
            _listenerThread?.Interrupt();
            _listenerThread = null;

            // Close all active connections
            foreach (var client in _clients) {
                var tcpClient = client.Value.client;

                try {
                    tcpClient.GetStream().Close();
                }
                catch { }

                tcpClient.Close();
            }

            _clients.Clear();
        }

        private void ListenThread(IPAddress address, int port) {
            try {
                // Start listener
                listener = new TcpListener(address, port);
                try {
                    listener.Start();
                }
                catch (Exception ex) {
                    Debug.LogError("Failed to start server: " + ex);
                    return;
                }

                // Accept new clients.
                while (true) {
                    // Accept a new client
                    var tcpClient = listener.AcceptTcpClient();

                    // Get client ID
                    var connectionID = GetConnectionID();

                    // Add to clients dictionary
                    var client = new Client(tcpClient);
                    _clients[connectionID] = client;

                    // Spawn send thread
                    var sendThread = new Thread(() => {
                                                    try {
                                                        SendThread(connectionID, client.client, client.dataQueue,
                                                                   client.dataPending);
                                                    }
                                                    catch (ThreadAbortException) {
                                                        // Do nothing
                                                    }
                                                    catch (Exception ex) {
                                                        Debug.LogError("Send thread exception: " + ex);
                                                    }
                                                });
                    sendThread.IsBackground = true;
                    sendThread.Start();

                    // Spawn recieve thread.
                    var receiveThread = new Thread(() => {
                                                       try {
                                                           // Run recieve loop
                                                           ReceiveThread(connectionID, client.client, recieveQueue,
                                                                         MaxMessageSize);

                                                           // Remove from client list.
                                                           _clients.TryRemove(connectionID, out var _);

                                                           // Stop send thread.
                                                           sendThread.Interrupt();
                                                       }
                                                       catch (Exception ex) {
                                                           Debug.LogError("Recieve thread exception: " + ex);
                                                       }
                                                   });
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
            }
            catch (ThreadAbortException) {
                // Unity did this, ignore
            }
            catch (SocketException) {
                // Happens on disconnect, ignore
            }
            catch (Exception ex) {
                // Finally, an actual issue
                Debug.LogError("Server listener thread exception: " + ex);
            }
        }

        public bool Send(int connectionID, byte[] data) {
            if (data.Length < MaxMessageSize) {
                // Get the client
                Client client;
                if (_clients.TryGetValue(connectionID, out client)) {
                    // Add to data queue and interrupt sending thread for this client
                    client.dataQueue.Enqueue(data);
                    client.dataPending.Set();
                    return true;
                }

                return false;
            }

            Debug.LogError("Data was too long!");
            return false;
        }

        public void Disconnect(int connectionID) {
            // Get client
            if (_clients.TryGetValue(connectionID, out var client)) {
                // Clean up connections.
                client.client.GetStream().Close();
                client.client.Close();

                // Disconnect message
                recieveQueue.Enqueue(new Message(connectionID, EventType.Disconnect, null));

                // Remove from client list.
                _clients.TryRemove(connectionID, out _);
            }
        }
    }
}