using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FNNLib.Transports.TCP;
using UnityEngine;
using EventType = FNNLib.Transports.TCP.EventType;

namespace FNNLib.Transports {
    // TODO: Migrate to new Transport system and rename the TCP Transport to something else? (Might wait until TCP rewrite however before naming)
    public class TCPTransport : Transport {
        #region General

        public override bool supported => Application.platform != RuntimePlatform.WebGLPlayer;
        
        public string serverListenAddress = "0.0.0.0";
        public ushort port = 7777;

        public int clientMaxReceivesPerUpdate = 1000;
        public int serverMaxReceivesPerUpdate = 10000;
        
        private TCPClient _client = new TCPClient();
        private TCPServer _server = new TCPServer();
        
        #endregion
        
        #region Client
        
        public override bool clientConnected => _client.Connected;
        
        public override void ClientConnect(string hostname) => _client.Connect(hostname, port);
        
        public override bool ClientSend(ArraySegment<byte> data, int channel) {
            var copy = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, copy, 0, data.Count);
            return _client.Send(copy);
        }
        
        public override void ClientDisconnect() => _client.Disconnect();

        private bool ProcessClientIncoming() {
            if (_client.TryGetMessage(out var msg)) {
                switch (msg.eventType) {
                    case EventType.Connect:
                        onClientConnected.Invoke();
                        break;
                    case EventType.Data:
                        onClientDataReceived.Invoke(new ArraySegment<byte>(msg.data));
                        break;
                    default:
                        onClientDisconnected.Invoke();
                        break; // Error = disconnect, so fire disconnect event!
                }

                return true;
            }

            return false;
        }
        
        #endregion

        #region Server
        
        public override bool serverRunning => _server.running;

        public override void ServerStart() {
            if (serverRunning)
                throw new NotSupportedException("Server is already running!");
            _server.Start(IPAddress.Parse(serverListenAddress), port);
        }

        public override bool ServerSend(List<ulong> clients, ArraySegment<byte> data, int channel = 0) {
            var copy = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, copy, 0, data.Count);
            
            foreach (var clientID in clients) {
                // Cannot send to server.
                if (clientID == 0) {
                    continue;
                }

                var id = GetTCPConnectionID(clientID);
                if (!_server.Send(id, copy))
                    return false;
            }

            return true;
        }

        public override void ServerDisconnect(ulong clientID) {
            if (clientID == 0)
                throw new NotSupportedException("You cannot disconnect the server!");
            _server.Disconnect(GetTCPConnectionID(clientID));
        }

        public override void ServerShutdown() {
            if (!serverRunning)
                throw new NotSupportedException("The server is already running!");
            _server.Stop();
        }

        private bool ProcessServerIncoming() {
            if (_server.TryGetMessage(out var msg)) {
                // Get client ID
                var clientID = GetFNNClientID(msg.clientID, false);
                switch (msg.eventType) {
                    case EventType.Connect:
                        onServerConnected?.Invoke(clientID);
                        break;
                    case EventType.Data:
                        onServerDataReceived.Invoke(clientID, new ArraySegment<byte>(msg.data));
                        break;
                    case EventType.Disconnect:
                    default: // Error = disconnect, so fire disconnect event!
                        onServerDisconnected?.Invoke(clientID);
                        break;
                }

                return true;
            }

            return false;
        }

        #endregion

        #region Lifecycle

        public override void Shutdown() {
            _client.Disconnect();
            _server.Stop();
        }

        private void LateUpdate() {
            if (!enabled)
                return;

            for (var i = 0; i < clientMaxReceivesPerUpdate; i++) {
                if (!ProcessClientIncoming() || !enabled)
                    break;
            }

            for (var i = 0; i < serverMaxReceivesPerUpdate; i++) {
                if (!ProcessServerIncoming() || !enabled)
                    break;
            }
        }
        
        #endregion
        
        #region Client IDs

        private int GetTCPConnectionID(ulong id) {
            if (id == 0)
                throw new NotSupportedException("Cannot convert server ID to TCP Client ID!");
            return (int) (id - 1);
        }

        public ulong GetFNNClientID(int id, bool isServer) {
            if (isServer)
                return 0;
            return (ulong) (id + 1);
        }
        
        #endregion
    }
}