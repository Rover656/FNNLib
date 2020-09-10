using System;
using System.IO;
using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.Messaging {
    /// <summary>
    /// Denotes a packet that is handled on the client.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ClientPacketAttribute : Attribute {}
    
    /// <summary>
    /// Denotes a packet that is handled on the server.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ServerPacketAttribute : Attribute {}
    
    public interface IPacket {
        void Serialize(NetworkWriter writer);
        void DeSerialize(NetworkReader reader);
    }
}