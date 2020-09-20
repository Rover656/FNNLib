using System;

namespace FNNLib.Messaging {
    /// <summary>
    /// A packet that can be buffered.
    /// </summary>
    public interface IBufferablePacket {
        /// <summary>
        /// Called when the packet has the option to be buffered.
        /// </summary>
        /// <returns>Whether the packet was buffered or not.</returns>
        bool BufferPacket(ulong sender);
    }
}