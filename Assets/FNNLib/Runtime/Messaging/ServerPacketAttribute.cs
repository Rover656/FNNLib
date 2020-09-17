using System;

namespace FNNLib.Messaging {
    /// <summary>
    /// Denotes a packet that is handled on the server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ServerPacketAttribute : Attribute {}
}