using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace FNNLib.Transports {
    /// <summary>
    /// 
    /// </summary>
    public abstract class Transport : MonoBehaviour {
        #region General

        /// <summary>
        /// Whether or not the transport is supported on this target platform.
        /// </summary>
        public abstract bool supported { get; }

        #endregion

        #region Client
        
        /// <summary>
        /// Whether the client is connected to the server.
        /// </summary>
        public abstract bool clientConnected { get; }

        /// <summary>
        /// Connect the client to the server.
        /// </summary>
        /// <param name="hostname">The server's hostname</param>
        public abstract void ClientConnect(string hostname);

        /// <summary>
        /// Send data to the server.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="channel">The channel to send the data down. Default = 0. Ignored on Transports that don't support channelling.</param>
        /// <returns>Whether the data was sent/queued to send.</returns>
        public abstract bool ClientSend(ArraySegment<byte> data, int channel = 0);

        /// <summary>
        /// Disconnect the client from the server.
        /// </summary>
        public abstract void ClientDisconnect();

        #endregion

        #region Server

        /// <summary>
        /// Whether the server is running.
        /// </summary>
        public abstract bool serverRunning { get; }

        /// <summary>
        /// Start the server.
        /// </summary>
        public abstract void ServerStart();

        /// <summary>
        /// Send data to clients.
        /// </summary>
        /// <param name="clients">The clients to send to.</param>
        /// <param name="data">The data to send.</param>
        /// <param name="channel">The channel to send the data down. Ignored on Transports that don't support channelling.</param>
        /// <param name="excludedClient"></param>
        /// <returns>Whether the data could be sent.</returns>
        public abstract bool ServerSend(List<ulong> clients, ArraySegment<byte> data, int channel, ulong excludedClient = 0);

        /// <summary>
        /// Force disconnect client.
        /// </summary>
        /// <remarks>
        /// You should use the higher level NetworkServer disconnect system, as that allows disconnection reasons.
        /// This is a last resort which is used by NetworkServer disconnect if the client doesn't honour it.
        /// </remarks>
        /// <param name="clientID">Client to force disconnect.</param>
        public abstract void ServerDisconnect(ulong clientID);

        /// <summary>
        /// Stop the server.
        /// </summary>
        public abstract void ServerShutdown();

        #endregion

        #region Lifecycle

        public abstract NetworkEventType GetMessage(out ulong clientID, out ArraySegment<byte> data, out int channel);

        /// <summary>
        /// Shutdown both client and server.
        /// </summary>
        public abstract void Shutdown();

        #endregion
    }
}