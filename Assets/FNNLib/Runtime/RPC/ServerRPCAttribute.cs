﻿using System;

namespace FNNLib.RPC {
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ServerRPCAttribute : RPCAttribute {
        /// <summary>
        /// Whether or not the caller must have ownership over the object.
        /// </summary>
        public bool requireOwnership = true;
    }
}