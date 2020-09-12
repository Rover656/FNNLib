using System;
using System.Collections.Generic;
using UnityEngine;

namespace FNNLib.Transports {
    /// <summary>
    /// TODO: Actually implement. This is simply an example of how a RUDP transport would work.
    /// </summary>
    public class RufflesTransport : Transport {
        /// <summary>
        /// The address to bind the server to.
        /// </summary>
        [Header("Connection Config")]
        public string serverBindAddress = "0.0.0.0";
        
        /// <summary>
        /// The default port.
        /// </summary>
        public int port = 7777;
        
        /// <summary>
        /// The channels and their types.
        /// 0 is default.
        /// </summary>
        public List<ChannelType> channels = new List<ChannelType> {ChannelType.ReliableSequenced};

        // Ruffles does not support WebGL
        public override bool supported => Application.platform != RuntimePlatform.WebGLPlayer;
        
        public override bool clientConnected { get; }
        
        public override void ClientConnect(string hostname) {
            throw new NotImplementedException();
        }

        public override bool ClientSend(ArraySegment<byte> data, int channel = 0) {
            throw new NotImplementedException();
        }

        public override void ClientDisconnect() {
            throw new NotImplementedException();
        }

        public override bool serverRunning { get; }
        
        public override void StartServer() {
            throw new NotImplementedException();
        }

        public override bool ServerSend(List<ulong> clients, ArraySegment<byte> data, int channel = 0) {
            throw new NotImplementedException();
        }

        public override void ServerDisconnect(ulong clientID) {
            throw new NotImplementedException();
        }

        public override void StopServer() {
            throw new NotImplementedException();
        }

        public override void Shutdown() {
            throw new NotImplementedException();
        }
    }
}