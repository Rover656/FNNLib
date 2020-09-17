using System;

namespace FNNLib.RPC {
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ClientRPCAttribute : RPCAttribute { }
}