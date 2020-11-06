using System;
using System.Reflection;
using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.RPC {
    internal class RPCMethod {
        internal readonly MethodInfo method;
        internal readonly bool useDelegate;
        internal readonly bool serverTarget;
        private readonly bool _requireOwnership;
        private readonly int _index;
        private readonly Type[] _parameterTypes;
        private readonly object[] _parameterRefs;

        internal static RPCMethod Create(MethodInfo method, ParameterInfo[] parameters, int index) {
            var attributes = (RPCAttribute[]) method.GetCustomAttributes(typeof(RPCAttribute), true);

            if (attributes.Length == 0)
                return null;
            if (attributes.Length > 1)
                Debug.LogWarning("More than one RPC attribute found, ignoring extras.");

            // Ensure the response can be serialized
            if (method.ReturnType != typeof(void) && !SerializationSystem.CanSerialize(method.ReturnType))
                return null;

            return new RPCMethod(method, parameters, attributes[0], index);
        }
        
        /// <summary>
        /// Create an RPC Method.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="parameters"></param>
        /// <param name="attribute"></param>
        /// <param name="index"></param>
        private RPCMethod(MethodInfo method, ParameterInfo[] parameters, RPCAttribute attribute, int index) {
            // Store method and our index in the delegate method list.
            this.method = method;
            _index = index;

            // Deal with permissions and targets.
            if (attribute is ServerRPCAttribute serverRPCAttribute) {
                serverTarget = true;
                _requireOwnership = serverRPCAttribute.requireOwnership;
            }  else {
                serverTarget = false;
                _requireOwnership = false;
            }

            // Deal with invocation.
            if (parameters.Length == 2 && method.ReturnType == typeof(void) &&
                parameters[0].ParameterType == typeof(ulong) && parameters[1].ParameterType == typeof(NetworkWriter)) {
                useDelegate = true;
            } else {
                useDelegate = false;
                _parameterTypes = new Type[parameters.Length];
                _parameterRefs = new object[parameters.Length];

                for (var i = 0; i < parameters.Length; i++)
                    _parameterTypes[i] = parameters[i].ParameterType;
            }
        }

        /// <summary>
        /// Invoke the RPC method
        /// </summary>
        /// <param name="target"></param>
        /// <param name="sender"></param>
        /// <param name="reader"></param>
        /// <returns></returns>
        internal object Invoke(NetworkBehaviour target, ulong sender, NetworkReader reader) {
            // Disallow running of the method.
            if (_requireOwnership && target.ownerClientID != sender)
                return null;
            
            // Save the invoking sender
            target._executingRPCSender = sender;

            if (!useDelegate)
                return InvokeReflected(target, reader);
            
            if (reader == null || reader.position == 0)
                return InvokeDelegate(target, sender, reader);

            var bufferRemaining = reader.length - reader.position;
            using (var dedicatedReader = NetworkReaderPool.GetReader(reader.ReadBytesSegment(bufferRemaining)))
                return InvokeReflected(target, dedicatedReader);
        }

        /// <summary>
        /// Invoke the reflected method.
        /// </summary>
        /// <param name="behaviour"></param>
        /// <param name="reader"></param>
        /// <returns></returns>
        private object InvokeReflected(NetworkBehaviour behaviour, NetworkReader reader) {
            for (var i = 0; i < _parameterTypes.Length; i++)
                _parameterRefs[i] = reader.ReadPackedObject(_parameterTypes[i]);
            return method.Invoke(behaviour, _parameterRefs);
        }

        /// <summary>
        /// Invoke a delegate method.
        /// This would be a method that takes a sender and a reader as parameters.
        /// </summary>
        /// <param name="behaviour"></param>
        /// <param name="sender"></param>
        /// <param name="reader"></param>
        /// <returns></returns>
        private object InvokeDelegate(NetworkBehaviour behaviour, ulong sender, NetworkReader reader) {
            behaviour.rpcDelegates[_index](sender, reader);
            return null;
        }
    }
}