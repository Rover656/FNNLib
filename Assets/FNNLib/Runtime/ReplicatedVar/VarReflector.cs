using System;
using System.Collections.Generic;
using FNNLib.Reflection;

namespace FNNLib.ReplicatedVar {
    internal class VarReflector : Reflector {
        private static readonly Dictionary<Type, VarReflector> typeLookup = new Dictionary<Type, VarReflector>();
        
        private List<IReplicatedVar> _replicatedVars;
        
        public VarReflector(Type type, NetworkBehaviour behaviour) {
            // Iterate over all fields and gather replicated vars
            _replicatedVars = new List<IReplicatedVar>();

            foreach (var field in GetFields(type, typeof(NetworkBehaviour))) {
                if (field.FieldType.HasInterface(typeof(IReplicatedVar))) {
                    var instance = (IReplicatedVar) field.GetValue(behaviour);
                    if (instance == null) {
                        instance = (IReplicatedVar) Activator.CreateInstance(field.FieldType, true);
                        field.SetValue(behaviour, instance);
                    }
                    
                    // TODO: Set networked behaviour
                    _replicatedVars.Add(instance);
                }
            }
        }

        public void UpdateVars() {
            
        }

        public void ReplicateFor(ulong clientID) {
            
        }
    }
}