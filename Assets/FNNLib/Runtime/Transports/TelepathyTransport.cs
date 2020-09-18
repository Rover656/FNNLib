using System;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace FNNLib.Transports {
    /// <summary>
    /// Transport using vis2k's Telepathy library.
    /// Currently the default transport for FNNLib. I want to write my own, however to get off the ground, using an existing lib is simpler and more supported.
    /// </summary>
    public class TelepathyTransport : Transport {
        #region General
        
        public ushort port = 7777;

        public bool noDelay = true;
        
        public int serverMaxMessageSize = 16 * 1024;
        
        public int serverMaxReceivesPerUpdate = 10000;

        public int clientMaxMessageSize = 16 * 1024;
        
        public int clientMaxReceivesPerUpdate = 1000;

        private Telepathy.Client _client = new Telepathy.Client();
        private Telepathy.Server _server = new Telepathy.Server();

        private void Awake() {
            // Route telepathy logging to Unity
            Telepathy.Logger.Log = Debug.Log;
            Telepathy.Logger.LogWarning = Debug.LogWarning;
            Telepathy.Logger.LogError = Debug.LogError;
            
            // Configure
            _client.NoDelay = noDelay;
            _client.MaxMessageSize = clientMaxMessageSize;
            _server.NoDelay = noDelay;
            _server.MaxMessageSize = serverMaxMessageSize;
        }
        
        public override bool supported => Application.platform != RuntimePlatform.WebGLPlayer;
        
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
            if (_client.GetNextMessage(out var msg)) {
                switch (msg.eventType) {
                    case Telepathy.EventType.Connected:
                        onClientConnected.Invoke();
                        break;
                    case Telepathy.EventType.Data:
                        onClientDataReceived.Invoke(new ArraySegment<byte>(msg.data), DefaultChannels.Reliable);
                        break;
                    case Telepathy.EventType.Disconnected:
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
        
        public override bool serverRunning => _server.Active;

        public override void ServerStart() {
            if (serverRunning)
                throw new NotSupportedException("Server is already running!");
            _server.Start(port);
        }

        public override bool ServerSend(ulong clientID, ArraySegment<byte> data, int channel = DefaultChannels.Reliable) {
            var copy = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, copy, 0, data.Count);
            
            // Cannot send to server.
            if (clientID == 0) {
                return true;
            }

            var id = GetTelepathyConnectionID(clientID);
            if (!_server.Send(id, copy))
                return false;

            return true;
        }

        public override bool ServerSend(List<ulong> clients, ArraySegment<byte> data, int channel = 0) {
            var copy = new byte[data.Count];
            Array.Copy(data.Array, data.Offset, copy, 0, data.Count);
            
            foreach (var clientID in clients) {
                // Cannot send to server.
                if (clientID == 0) {
                    continue;
                }

                var id = GetTelepathyConnectionID(clientID);
                if (!_server.Send(id, copy))
                    return false;
            }

            return true;
        }

        public override void ServerDisconnect(ulong clientID) {
            if (clientID == 0)
                throw new NotSupportedException("You cannot disconnect the server!");
            _server.Disconnect(GetTelepathyConnectionID(clientID));
        }

        public override void ServerShutdown() {
            if (!serverRunning)
                throw new NotSupportedException("The server is already running!");
            _server.Stop();
        }

        private bool ProcessServerIncoming() {
            if (_server.GetNextMessage(out var msg)) {
                // Get client ID
                var clientID = GetFNNClientID(msg.connectionId, false);
                switch (msg.eventType) {
                    case Telepathy.EventType.Connected:
                        onServerConnected?.Invoke(clientID);
                        break;
                    case Telepathy.EventType.Data:
                        onServerDataReceived.Invoke(clientID, new ArraySegment<byte>(msg.data), DefaultChannels.Reliable);
                        break;
                    case Telepathy.EventType.Disconnected:
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

        private static int GetTelepathyConnectionID(ulong id) {
            if (id == 0)
                throw new NotSupportedException("Cannot convert server ID to TCP Client ID!");
            return (int) id;
        }

        public static ulong GetFNNClientID(int id, bool isServer) {
            if (isServer)
                return 0;
            return (ulong) id;
        }
        
        #endregion
    }
}