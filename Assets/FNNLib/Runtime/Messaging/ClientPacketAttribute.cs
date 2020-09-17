using System;

namespace FNNLib.Messaging {
    /// <summary>
    /// Denotes a packet that is handled on the client.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ClientPacketAttribute : Attribute {}
}