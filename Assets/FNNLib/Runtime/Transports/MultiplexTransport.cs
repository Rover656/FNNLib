using System;
using System.Collections.Generic;
using UnityEngine;

namespace FNNLib.Transports {
    // TODO: Placeholder class. Will support the use of multiple transports for one server and will pick a transport to use for the client.
    public class MultiplexTransport : Transport {
        /// <summary>
        /// The transports that are combined.
        /// </summary>
        public List<Transport> transports;
        
        public override bool supported { get; }
        
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
        
        public override void ServerStart() {
            throw new NotImplementedException();
        }

        public override bool ServerSend(List<ulong> clients, ArraySegment<byte> data, int channel = 0) {
            throw new NotImplementedException();
        }

        public override void ServerDisconnect(ulong clientID) {
            throw new NotImplementedException();
        }

        public override void ServerShutdown() {
            throw new NotImplementedException();
        }

        public override void Shutdown() {
            throw new NotImplementedException();
        }
    }
}