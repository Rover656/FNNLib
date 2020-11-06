using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FNNLib.Reflection;
using FNNLib.SceneManagement;
using FNNLib.Serialization;
using FNNLib.Utilities;

namespace FNNLib.RPC {
    public delegate void RPCDelegate(ulong sender, NetworkReader reader);
    
    /// <summary>
    /// RPC Method Reflector.
    /// Collects and manages any RPC method.
    /// </summary>
    internal class RPCReflector : Reflector {
        /// <summary>
        /// Caches reflectors across all types to lower memory overhead.
        /// </summary>
        private static readonly Dictionary<Type, RPCReflector> typeLookup = new Dictionary<Type, RPCReflector>();

        public static RPCReflector GetOrCreate(Type type) {
            if (typeLookup.ContainsKey(type))
                return typeLookup[type];
            var data = new RPCReflector(type);
            typeLookup.Add(type, data);
            return data;
        }
        
        public readonly Dictionary<ulong, RPCMethod> serverMethods = new Dictionary<ulong, RPCMethod>();
        public readonly Dictionary<ulong, RPCMethod> clientMethods = new Dictionary<ulong, RPCMethod>();
        private readonly RPCMethod[] delegateMethods;
        
        private RPCReflector(Type type) {
            var delegateMethodList = new List<RPCMethod>();
            var methods = GetMethodsRecursive(type, typeof(NetworkBehaviour));

            foreach (var method in methods) {
                var parameters = method.GetParameters();
                var rpcMethod = RPCMethod.Create(method, parameters, delegateMethodList.Count);

                if (rpcMethod == null)
                    continue;

                var lookupTable = rpcMethod.serverTarget ? serverMethods : clientMethods;
                var hash = HashMethodNameAndValidate(method.Name);

                if (!lookupTable.ContainsKey(hash))
                    lookupTable.Add(hash, rpcMethod);

                if (parameters.Length > 0) {
                    var sigHash = HashMethodSignatureAndValidate(method);

                    if (!lookupTable.ContainsKey(sigHash)) {
                        lookupTable.Add(sigHash, rpcMethod);
                    }
                }

                if (rpcMethod.useDelegate) {
                    delegateMethodList.Add(rpcMethod);
                }
            }

            delegateMethods = delegateMethodList.ToArray();
        }
        
        /// <summary>
        /// Create delegates for a specified behaviour.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        internal RPCDelegate[] CreateTargetDelegates(NetworkBehaviour target) {
            if (delegateMethods.Length == 0)
                return null;
            
            var delegates = new RPCDelegate[delegateMethods.Length];
            for (var i = 0; i < delegateMethods.Length; i++)
                delegates[i] =
                    (RPCDelegate) Delegate.CreateDelegate(typeof(RPCDelegate), target, delegateMethods[i].method.Name);
            
            return delegates;
        }
        
        private static ulong HashMethodNameAndValidate(string name) {
            // TODO: Collision checking
            return NetworkBehaviour.HashMethodName(name);
        }

        private static ulong HashMethodSignatureAndValidate(MethodInfo method) {
            // TODO: Collision checking
            return NetworkBehaviour.HashMethodSignature(method);
        }
    }
}