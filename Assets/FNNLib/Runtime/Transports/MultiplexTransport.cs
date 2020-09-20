using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FNNLib.Transports {
    // TODO: Placeholder class. Will support the use of multiple transports for one server and will pick a transport to use for the client.
    public class MultiplexTransport : Transport {
        /// <summary>
        /// The transports that are combined.
        /// </summary>
        public Transport[] transports;

        public override bool supported => transports.Any(transport => transport.supported);

        private void Awake() {
            if (transports == null || transports.Length == 0)
                throw new NotSupportedException("You must provide at least 1 transport to Multiplex Transport!");
            
        }

        #region Client
        
        /// <summary>
        /// The transport that has been selected for the client
        /// </summary>
        private Transport _selectedTransport;

        public override bool clientConnected => _selectedTransport != null && _selectedTransport.clientConnected;

        public override void ClientConnect(string hostname) {
            foreach (var transport in transports) {
                if (transport.supported) {
                    _selectedTransport = transport;
                    transport.ClientConnect(hostname);
                }
            }
            throw new ArgumentException("No suitable transport could be found!");
        }

        public override bool ClientSend(ArraySegment<byte> data, int channel = DefaultChannels.Reliable) {
            return _selectedTransport.ClientSend(data, channel);
        }

        public override void ClientDisconnect() {
            if (_selectedTransport != null)
                _selectedTransport.ClientDisconnect();
        }
        
        #endregion

        public override bool serverRunning { get; }
        
        public override void ServerStart() {
            throw new NotImplementedException();
        }

        public override bool ServerSend(List<ulong> clients, ArraySegment<byte> data, int channel = 0, ulong excludedClient = 0) {
            throw new NotImplementedException();
        }

        public override void ServerDisconnect(ulong clientID) {
            throw new NotImplementedException();
        }

        public override void ServerShutdown() {
            throw new NotImplementedException();
        }

        public override NetworkEventType GetMessage(out ulong clientID, out ArraySegment<byte> data, out int channel) {
            throw new NotImplementedException();
        }

        public override void Shutdown() {
            throw new NotImplementedException();
        }
    }
}