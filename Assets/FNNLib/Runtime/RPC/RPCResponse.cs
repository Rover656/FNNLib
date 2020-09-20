namespace FNNLib.RPC { 
    /// <summary>
    /// A RPC response
    /// This wraps the base response and casts the result to the desired type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RPCResponse<T> : BaseRPCResponse {
        /// <summary>
        /// The response result.
        /// </summary>
        public T value { get; private set; }

        /// <summary>
        /// Internal setter for the result.
        /// </summary>
        internal override object result {
            set => this.value = (T) value;
        }
    }
}