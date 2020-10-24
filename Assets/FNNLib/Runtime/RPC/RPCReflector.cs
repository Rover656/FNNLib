using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FNNLib.Reflection;
using FNNLib.Serialization;
using FNNLib.Utilities;

namespace FNNLib.RPC {
    public delegate void RPCDelegate(ulong sender, NetworkReader reader);
    
    internal class RPCReflector : Reflector {
        private static readonly Dictionary<Type, RPCReflector> typeLookup = new Dictionary<Type, RPCReflector>();

        public static RPCReflector GetOrCreate(Type type) {
            if (typeLookup.ContainsKey(type))
                return typeLookup[type];
            var data = new RPCReflector(type);
            typeLookup.Add(type, data);
            return data;
        }
        
        public readonly Dictionary<ulong, RPCReflectedMethod> serverMethods = new Dictionary<ulong, RPCReflectedMethod>();
        public readonly Dictionary<ulong, RPCReflectedMethod> clientMethods = new Dictionary<ulong, RPCReflectedMethod>();
        private readonly RPCReflectedMethod[] delegateMethods;
        
        private RPCReflector(Type type) {
            var delegateMethodList = new List<RPCReflectedMethod>();
            var methods = GetMethodsRecursive(type, typeof(NetworkBehaviour));

            foreach (var method in methods) {
                var parameters = method.GetParameters();
                var rpcMethod = RPCReflectedMethod.Create(method, parameters, delegateMethodList.Count);

                if (rpcMethod == null)
                    continue;

                var lookupTable = rpcMethod.serverTarget ? serverMethods : clientMethods;
                var hash = HashMethodNameAndValidate(method.Name);

                if (!lookupTable.ContainsKey(hash))
                    lookupTable.Add(hash, rpcMethod);

                if (parameters.Length > 0) {
                    // TODO: Validate
                    var sigHash = NetworkBehaviour.HashMethodSignature(method);

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
    }
}