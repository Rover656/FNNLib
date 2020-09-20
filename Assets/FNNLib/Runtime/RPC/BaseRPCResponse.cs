using System;

namespace FNNLib.RPC {
    /// <summary>
    /// The base RPC response.
    /// This is a base class as we can't store templated classes.
    /// We can however store this and use properties to pass the value back to where it is understood.
    /// </summary>
    public abstract class BaseRPCResponse {
        /// <summary>
        /// The response ID.
        /// </summary>
        public ulong responseID { get; internal set; }
        
        /// <summary>
        /// Whether or not the response is recieved.
        /// </summary>
        public bool isDone { get; internal set; }
        
        /// <summary>
        /// Whether or not a value was received.
        /// </summary>
        public bool isSuccessful { get; internal set; }
        
        /// <summary>
        /// The client ID of the requester.
        /// </summary>
        public ulong clientID { get; internal set; }
        
        // TODO: Handle timeouts
        
        /// <summary>
        /// The result setter
        /// </summary>
        internal abstract object result { set; }
        
        /// <summary>
        /// The type of the result.
        /// Used for ReadPackedObject.
        /// </summary>
        internal Type resultType { get; set; }
    }
}