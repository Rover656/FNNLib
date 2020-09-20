using System.Collections.Generic;
using UnityEngine;

namespace FNNLib.RPC {
    /// <summary>
    /// Handler for RPC responses
    /// </summary>
    internal static class RPCResponseManager {
        /// <summary>
        /// All awaiting responses.
        /// </summary>
        private static readonly Dictionary<ulong, BaseRPCResponse> _pending = new Dictionary<ulong, BaseRPCResponse>();
        
        /// <summary>
        /// The times for every response. Used for timeouts.
        /// </summary>
        private static readonly SortedList<ulong, float> _responseTimes = new SortedList<ulong, float>();

        /// <summary>
        /// Response ID counter.
        /// </summary>
        private static ulong _idCounter;
        
        /// <summary>
        /// Get a response ID
        /// </summary>
        /// <returns></returns>
        internal static ulong GetResponseID() {
            return _idCounter++;
        }

        /// <summary>
        /// Add a response to the waiting queue.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="response"></param>
        internal static void Add(ulong id, BaseRPCResponse response) {
            _pending.Add(id, response);
            _responseTimes.Add(id, Time.unscaledTime);
        }

        /// <summary>
        /// Get a response from the queue
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static BaseRPCResponse Get(ulong id) {
            return _pending[id];
        }

        /// <summary>
        /// Remove a response from the queue
        /// </summary>
        /// <param name="id"></param>
        internal static void Remove(ulong id) {
            _pending.Remove(id);
            _responseTimes.Remove(id);
        }

        /// <summary>
        /// Whether or not the queue contains this response.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static bool Contains(ulong id) {
            return _pending.ContainsKey(id);
        }

        /// <summary>
        /// Handle a response packet.
        /// This will pass the response back to the caller.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="channel"></param>
        internal static void ClientHandleRPCResponse(RPCResponsePacket packet, int channel) {
            ServerHandleRPCResponse(NetworkManager.ServerLocalID, packet, channel);
        }
        
        internal static void ServerHandleRPCResponse(ulong clientID, RPCResponsePacket packet, int channel) {
            // If we have this response, finish it
            if (Contains(packet.responseID)) {
                var response = Get(packet.responseID);
                Remove(packet.responseID);

                response.isDone = true;
                response.isSuccessful = true;
                response.result = packet.result;
            }
        }
    }
}