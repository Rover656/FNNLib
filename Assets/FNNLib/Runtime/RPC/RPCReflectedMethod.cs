using System;
using System.Reflection;
using FNNLib.Serialization;

namespace FNNLib.RPC {
    internal class RPCReflectedMethod {
        internal readonly MethodInfo method;
        internal readonly bool useDelegate;
        internal readonly bool serverTarget;
        private readonly bool requireOwnership;
        private readonly int index;
        private readonly Type[] parameterTypes;
        private readonly object[] parameterRefs;
        
        internal RPCReflectedMethod(MethodInfo method, ParameterInfo[] parameters, RPCAttribute attribute, int index) {
            this.method = method;
            this.index = index;

            // TODO: Server RPCs
            if (attribute is ServerRPCAttribute serverRPCAttribute) {
                serverTarget = true;
                requireOwnership = serverRPCAttribute.requireOwnership;
            }  else {
                serverTarget = false;
                requireOwnership = false;
            }

            if (parameters.Length == 2 && method.ReturnType == typeof(void) &&
                parameters[0].ParameterType == typeof(ulong) && parameters[1].ParameterType == typeof(NetworkWriter)) {
                useDelegate = true;
            } else {
                useDelegate = false;
                parameterTypes = new Type[parameters.Length];
                parameterRefs = new object[parameters.Length];

                for (var i = 0; i < parameters.Length; i++)
                    parameterTypes[i] = parameters[i].ParameterType;
            }
        }

        internal static RPCReflectedMethod Create(MethodInfo method, ParameterInfo[] parameters, int index) {
            var attributes = (RPCAttribute[]) method.GetCustomAttributes(typeof(RPCAttribute), true);

            if (attributes.Length == 0)
                return null;

            // TODO: The ability to have return types.
            if (method.ReturnType != typeof(void))
                return null;

            return new RPCReflectedMethod(method, parameters, attributes[0], index);
        }

        internal object Invoke(NetworkBehaviour target, ulong sender, NetworkReader reader) {
            // Disallow running of the method.
            if (requireOwnership && target.ownerClientID != sender)
                return null;
            
            // TODO: Save invoking sender

            if (reader == null || reader.position == 0) {
                return useDelegate ? InvokeDelegate(target, sender, reader) : InvokeReflected(target, reader);
            }
            
            using (var dedicatedReader =
                NetworkReaderPool.GetReader(reader.ReadBytesSegment(reader.length - reader.position))) {
                return useDelegate ? InvokeDelegate(target, sender, dedicatedReader) : InvokeReflected(target, dedicatedReader);
            }
        }

        private object InvokeReflected(NetworkBehaviour behaviour, NetworkReader reader) {
            for (var i = 0; i < parameterTypes.Length; i++)
                parameterRefs[i] = reader.ReadPackedObject(parameterTypes[i]);
            return method.Invoke(behaviour, parameterRefs);
        }

        private object InvokeDelegate(NetworkBehaviour behaviour, ulong sender, NetworkReader reader) {
            behaviour.rpcDelegates[index](sender, reader);
            return null;
        }
    }
}