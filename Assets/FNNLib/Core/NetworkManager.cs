﻿using System;
using FNNLib.Messaging;
using FNNLib.Transports;
using UnityEngine;

namespace FNNLib.Core {
    /// <summary>
    /// The network manager drives the NetworkClient and NetworkServer systems.
    ///
    /// TODO: Add a way of bulk adding packets to either client or server based on config.
    /// </summary>
    public class NetworkManager : MonoBehaviour {
        /// <summary>
        /// The game's NetworkManager.
        /// </summary>
        public static NetworkManager Instance;
        
        /// <summary>
        /// The selected transport to be used by the client and server.
        /// </summary>
        public Transport transport;
        
        /// <summary>
        /// Whether or not the game should run clientside code.
        /// </summary>
        public bool isClient { get; internal set; }
        
        /// <summary>
        /// Whether or not the game should run serverside code.
        /// </summary>
        public bool isServer { get; internal set; }
        
        /// <summary>
        /// Whether or not the client is a virtual client.
        /// </summary>
        public bool isHost => isClient && isServer;

        /// <summary>
        /// The local client ID.
        /// </summary>
        public int localClientID => isServer ? transport.serverClientID : _client.localClientID;
        
        /// <summary>
        /// The server we are controlling.
        /// This can be accessed when the Server is running with NetworkServer.Instance.
        /// </summary>
        private NetworkServer _server = new NetworkServer();
        
        /// <summary>
        /// The client we are controlling.
        /// This can be accessed when the Client is running with NetworkClient.Instance.
        /// </summary>
        private NetworkClient _client = new NetworkClient();

        private void Awake() {
            if (Instance != null && Instance == this) {
                Debug.LogError("Only one NetworkManager may exist. Destroying.");
                Destroy(this);
            } else if (Instance == null) Instance = this;

            // TODO: Add packets for the features I add, such as RPCs and Networked Variables etc.
        }

        private void OnDestroy() {
            // Close host/server/client on destroy!
            if (isHost)
                StopHost();
            else if (isServer)
                StopServer();
            else if (isClient)
                StopClient();
        }
        
        #region Client

        public void StartClient(string hostname) {
            // Ensure manager isn't running.
            if (isHost)
                throw new NotSupportedException("The network manager is already running in host mode!");
            if (isServer)
                throw new NotSupportedException("The network manager is already running in server mode!");
            if (isClient)
                throw new NotSupportedException("A client is already running!");
            
            // Activate transport
            transport.StartUsing();

            // Start client
            isClient = true;
            _client.Connect(hostname);
        }

        /// <summary>
        /// Stop client mode
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if running in a different mode, or a client isn't running.</exception>
        public void StopClient() {
            if (isHost)
                throw new NotSupportedException("The network manager is running in host mode! Use StopHost().");
            if (isServer)
                throw new NotSupportedException("The network manager is running in server mode! Use StopServer().");
            if (!isClient)
                throw new NotSupportedException("A client is not running!");

            // Disconnect
            _client.Disconnect();
            isClient = false;
            
            // Deactivate transport
            transport.StopUsing();
        }
        
        #endregion
        
        #region Server

        /// <summary>
        /// Starts the manager in server mode.
        /// </summary>
        public void StartServer() {
            if (isHost)
                throw new NotSupportedException("The network manager is already running in host mode!");
            if (isClient)
                throw new NotSupportedException("The network manager is already running in client mode!");
            if (isServer)
                throw new NotSupportedException("A server is already running!");

            // Start transport
            transport.StartUsing();
            
            // Start server.
            isServer = true;
            _server.Start();
            
            // Hook stop event in case it closes.
            _server.OnServerStopped += OnServerStopped;
        }
        
        /// <summary>
        /// Stop a running server.
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        public void StopServer() {
            if (isHost)
                throw new NotSupportedException("The network manager is running in host mode! Use StopHost() instead.");
            if (isClient)
                throw new NotSupportedException("The network manager is running in client mode! Use StopClient() instead.");
            if (!isServer)
                throw new NotSupportedException("A server is not running!");
            
            // Stop server
            _server.Stop();
        }

        private void OnServerStopped(NetworkServer server) {
            // Remove hooks
            server.OnServerStopped -= OnServerStopped;
            isServer = false;
            
            // Disable transport
            transport.StopUsing();
        }
        
        #endregion

        #region Host

        public void StartHost() {
            if (isClient && !isServer)
                throw new NotSupportedException("The network manager is already running in client mode!");
            if (isServer && !isClient)
                throw new NotSupportedException("The network manager is already running in server mode!");
            if (isHost)
                throw new NotSupportedException("Host mode is already running!");
            
            // TODO: Host mode implementation
        }

        public void StopHost() {
            if (isClient && !isServer)
                throw new NotSupportedException("The network manager is running in client mode! Use StopHost().");
            if (isServer && !isClient)
                throw new NotSupportedException("The network manager is running in server mode! Use StopServer().");
            if (!isHost)
                throw new NotSupportedException("A client is not running!");
            
            // TODO: Host mode implementation
        }
        
        #endregion
        
        #region Single player
        
        // TODO: In the future, I could add a single player, which will simply do the same as host, without running a networked server at all.
        //        It wouldn't take much because host already deals with the propogation of events around the virtual client, we just need to turn off the networked side completely. 
        
        #endregion
        
        #region Sending Data
        
        /// <summary>
        /// Send data through the network manager.
        /// </summary>
        /// <param name="clientID">Target client ID. Ignored if running as client.</param>
        /// <param name="packet">The packet to send.</param>
        /// <typeparam name="T">The packet type.</typeparam>
        public void Send<T>(int clientID, T packet) where T : IPacket, new() {
            // Don't send if we are running in host mode.
            if (isServer && clientID == Transport.currentTransport.serverClientID)
                return;
            if (isServer)
                _server.Send(clientID, packet);
            else _client.Send(packet);
        }
        
        #endregion
        
        #region Registering Packets

        /// <summary>
        /// Registers a packet on the client and server, depending on it's target.
        /// </summary>
        /// <param name="handler"></param>
        /// <typeparam name="T"></typeparam>
        public void RegisterPacketHandler<T>(Action<int, T> handler) where T : IPacket, new() {
            if (PacketUtils.IsClientPacket<T>()) {
                _client.RegisterPacketHandler<T>(handler);
            }
            
            if (PacketUtils.IsServerPacket<T>()) {
                _server.RegisterPacketHandler<T>(handler);
            }
        }
        
        #endregion
    }
}