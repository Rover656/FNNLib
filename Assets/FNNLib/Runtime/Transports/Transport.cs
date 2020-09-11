using System;
using UnityEngine;
using UnityEngine.Events;

namespace FNNLib.Transports {
    public delegate void TransportClientConnected();
    
    /// <summary>
    /// A transport contains the underlying implementation of a network connection.
    /// TODO: Implement channels for UDP support.
    /// TODO: Make this transport just have a create method for an internal client and server? I think I prefer that to a mess of methods and fields in one object.
    /// </summary>
    public abstract class Transport : MonoBehaviour {
        /// <summary>
        /// The transport currently in use.
        /// </summary>
        public static Transport currentTransport = null;
        
        /// <summary>
        /// The client ID that represents the server.
        /// </summary>
        public virtual int serverClientID => 0;
        
        /// <summary>
        /// Whether or not the transport is supported on this platform.
        /// </summary>
        public abstract bool supported { get; }
        
        /// <summary>
        /// Whether or not the client is connected.
        /// </summary>
        public abstract bool clientConnected { get; }
        
        /// <summary>
        /// Whether or not the server is running
        /// </summary>
        public abstract bool serverRunning { get; }
        
        /// <summary>
        /// Start using this transport.
        /// Only one transport can be in use at once.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if a transport is already in use.</exception>
        public void StartUsing() {
            if (currentTransport != null)
                throw new NotSupportedException("A transport is already in use!");
            currentTransport = this;
        }

        /// <summary>
        /// Stop using this transport.
        /// Only one transport may be in use at once, use this to stop using it.
        /// Must not be running a client or server.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown if the transport is not in use, a client is still connected or a server is still running.</exception>
        public void StopUsing() {
            if (currentTransport != this)
                throw new NotSupportedException("This transport is not in use!");
            if (clientConnected)
                throw new NotSupportedException("You must disconnect the client before stopping this transport!");
            if (serverRunning)
                throw new NotSupportedException("You must stop the server before stopping this transport!");
            currentTransport = null;
        }
        
        #region Server
        
        // TODO: Implement these, it will help tidy NetworkServer.
        public UnityEvent onServerStarted = new UnityEvent();
        public UnityEvent onServerStopped = new UnityEvent();

        /// <summary>
        /// Fired when a client connects to the server.
        /// Parameters: clientID
        /// </summary>
        public UnityEvent<int> onServerConnected = new UnityEvent<int>();
        
        /// <summary>
        /// Fired when a client disconnects from the server.
        /// Parameters: clientID
        /// </summary>
        public UnityEvent<int> onServerDisconnected = new UnityEvent<int>();
        
        /// <summary>
        /// Fired when the server recieves data from a client.
        /// Parameters: clientID, data
        /// </summary>
        public UnityEvent<int, ArraySegment<byte>> onServerDataReceived = new UnityEvent<int, ArraySegment<byte>>();

        /// <summary>
        /// Start a server using this transport.
        /// </summary>
        public abstract void StartServer();
        
        /// <summary>
        /// Stop a server using this transport.
        /// </summary>
        public abstract void StopServer();
        
        /// <summary>
        /// Send data through the server.
        /// </summary>
        /// <param name="clientID">Destination client.</param>
        /// <param name="data">Data to send.</param>
        /// <returns>Whether the data was sent successfully.</returns>
        public abstract bool ServerSend(int clientID, ArraySegment<byte> data);

        /// <summary>
        /// Disconnect a client from the server.
        /// </summary>
        /// <param name="clientID">The client to disconnect.</param>
        public abstract void ServerDisconnectClient(int clientID);
        
        #endregion

        #region Client

        /// <summary>
        /// Fired when the client connects to the server.
        /// </summary>
        public UnityEvent onClientConnected = new UnityEvent();
        
        /// <summary>
        /// Fired when receiving data from the server.
        /// </summary>
        public UnityEvent<ArraySegment<byte>> onClientDataReceived = new UnityEvent<ArraySegment<byte>>();

        /// <summary>
        /// Fired when the client disconnects from the server.
        /// </summary>
        public UnityEvent onClientDisconnected = new UnityEvent();

        /// <summary>
        /// Start a client and connect to the server.
        /// </summary>
        /// <param name="hostname">The server's hostname</param>
        public abstract void StartClient(string hostname);
        
        /// <summary>
        /// Stop the client (disconnect from the server).
        /// </summary>
        public abstract void StopClient();
        
        /// <summary>
        /// Send data to the server.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public abstract bool ClientSend(ArraySegment<byte> data);
        
        #endregion
    }
}