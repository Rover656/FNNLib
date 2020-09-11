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
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace FNNLib.Transports.TCP {
    public class TCPClient : CommonBackend {
        /// <summary>
        /// The underlying client.
        /// </summary>
        public TcpClient client;

        private Thread _recieveThread;
        private Thread _sendThread;

        private bool _connecting;

        private SafeQueue<byte[]> dataQueue = new SafeQueue<byte[]>();
        private ManualResetEvent dataPending = new ManualResetEvent(false);

        public bool Connected => client != null &&
                                 client.Client != null &&
                                 client.Client.Connected;

        public void Connect(string hostname, int port) {
            if (Connected || _connecting) return;
            client = new TcpClient {Client = null};
            _connecting = true;
            
            // Start receive thread which will start the connection process
            _recieveThread = new Thread(() => { RecieveThread(hostname, port); });
            _recieveThread.IsBackground = true;
            _recieveThread.Start();
        }

        public bool Send(byte[] data) {
            if (data.Length < MaxMessageSize) {
                // Add to data queue and interrupt sending thread for this client
                dataQueue.Enqueue(data);
                dataPending.Set();
                return true;
            }

            Debug.LogError("Data was too long!");
            return false;
        }

        public void Disconnect() {
            if (Connected || _connecting) {
                // Close client
                client.Close();
                
                // Kill receive thread
                _recieveThread?.Interrupt();
                
                // Reset connecting
                _connecting = false;
                
                // Reset sending queue
                dataQueue.Clear();

                // Set client to null
                client = null;
            }
        }

        private void RecieveThread(string hostname, int port) {
            try {
                // Attempt connection
                client.Connect(hostname, port);
                _connecting = false;
                
                // Start send thread
                _sendThread = new Thread(() => { SendThread(0, client, dataQueue, dataPending); });
                _sendThread.IsBackground = true;
                _sendThread.Start();
                
                // Run the recieve loop
                ReceiveThread(0, client, recieveQueue, MaxMessageSize);
            }
            catch (Exception ex) {
                // TODO: Alright exceptions need filtered
                Debug.Log("Error in client receive thread: " + ex);
            }
            
            // Kill sending thread
            _sendThread?.Interrupt();
            
            // Mark as not connecting (because it may have failed)
            _connecting = false;
            
            // Close the client
            client?.Close();
        }
    }
}