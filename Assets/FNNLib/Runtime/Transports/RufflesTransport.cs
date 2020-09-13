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
        [Header("Connection Config")] public string serverBindAddress = "0.0.0.0";

        /// <summary>
        /// The default port.
        /// </summary>
        public int port = 7777;

        /// <summary>
        /// The channels and their types.
        /// The first 3 channels are reserved as defaults.
        /// </summary>
        public ChannelType[] channels = {ChannelType.Reliable, ChannelType.ReliableSequenced, ChannelType.Unreliable};

        // Ruffles does not support WebGL
        public override bool supported => Application.platform != RuntimePlatform.WebGLPlayer;

        private void OnValidate() {
            // Ensure the first 3 channels align with default channels.
            if (channels == null || channels.Length >= 3) {
                if (channels[0] != ChannelType.Reliable) channels[0] = ChannelType.Reliable;
                if (channels[1] != ChannelType.ReliableSequenced) channels[1] = ChannelType.ReliableSequenced;
                if (channels[2] != ChannelType.Unreliable) channels[2] = ChannelType.Unreliable;
            }
            else {
                channels = new[] {
                                     ChannelType.ReliableSequenced,
                                     ChannelType.Reliable,
                                     ChannelType.Unreliable
                                 };
            }
        }

        public override bool clientConnected { get; }

        public override void ClientConnect(string hostname) {
            throw new NotImplementedException();
        }

        public override bool ClientSend(ArraySegment<byte> data, int channel = DefaultChannels.Reliable) {
            throw new NotImplementedException();
        }

        public override void ClientDisconnect() {
            throw new NotImplementedException();
        }

        public override bool serverRunning { get; }

        public override void ServerStart() {
            throw new NotImplementedException();
        }

        public override bool ServerSend(List<ulong> clients, ArraySegment<byte> data, int channel = DefaultChannels.Reliable) {
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