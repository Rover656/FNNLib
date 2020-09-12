using System;
using System.Net;
using FNNLib.Transports.TCP;
using UnityEngine;
using EventType = FNNLib.Transports.TCP.EventType;

namespace FNNLib.Transports {
    // TODO: Migrate to new Transport system and rename the TCP Transport to something else? (Might wait until TCP rewrite however before naming)
    public class TCPTransport : LegacyTransport {
        public override bool supported => Application.platform != RuntimePlatform.WebGLPlayer;
        public override bool clientConnected => _client.Connected;
        public override bool serverRunning => _server.running;

        public string serverListenAddress = "0.0.0.0";
        public ushort port = 7777;

        public int clientMaxReceivesPerUpdate = 1000;
        public int serverMaxReceivesPerUpdate = 10000;

        private TCPClient _client = new TCPClient();
        private TCPServer _server = new TCPServer();

        #region Server
        public override void StartServer() => _server.Start(IPAddress.Parse(serverListenAddress), port);
        public override void StopServer() => _server.Stop();

        public override bool ServerSend(int clientID, ArraySegment<byte> data) {
            var copy = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, copy, 0, data.Count);
            return _server.Send(clientID, copy);
        }

        public override void ServerDisconnectClient(int clientID) {
            _server.Disconnect(clientID);
        }

        private bool ProcessServerIncoming() {
            if (_server.TryGetMessage(out var msg)) {
                switch (msg.eventType) {
                    case EventType.Connect:
                        onServerConnected?.Invoke(msg.connectionID);
                        break;
                    case EventType.Data:
                        onServerDataReceived.Invoke(msg.connectionID, new ArraySegment<byte>(msg.data));
                        break;
                    case EventType.Disconnect:
                    default: // Error = disconnect, so fire disconnect event!
                        onServerDisconnected?.Invoke(msg.connectionID);
                        break;
                }

                return true;
            }

            return false;
        }

        #endregion
        
        #region Client
        public override void StartClient(string hostname) => _client.Connect(hostname, port);
        public override void StopClient() => _client.Disconnect();
        
        public override bool ClientSend(ArraySegment<byte> data) {
            var copy = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, copy, 0, data.Count);
            return _client.Send(copy);
        }

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
        
        #region Lifecycle

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
    }
}