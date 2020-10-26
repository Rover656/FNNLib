using System;
using System.Collections.Generic;
using FNNLib.Messaging;
using FNNLib.Reflection;
using FNNLib.Serialization;

namespace FNNLib.ReplicatedVar {
    // TODO: Defer into two different classes, one for reflection and one for managing vars.
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
                    
                    // Set network behaviour
                    instance.SetNetworkBehaviour(behaviour);
                    
                    // Add to list
                    _replicatedVars.Add(instance);
                }
            }
        }

        // TODO: Client read and protection
        
        public bool AnyDirty(bool isServer, ulong clientId) {
            for (var i = 0; i < _replicatedVars.Count; i++) {
                if (_replicatedVars[i].IsDirty() && (isServer || _replicatedVars[i].CanClientWrite(clientId))) {
                    return true;
                }
            }

            return false;
        }

        public void WriteValues(NetworkWriter writer) {
            writer.WritePackedInt32(_replicatedVars.Count);
            for (var i = 0; i < _replicatedVars.Count; i++) {
                _replicatedVars[i].Write(writer);
            }
        }

        public void WriteDeltas(NetworkWriter writer) {
            for (var i = 0; i < _replicatedVars.Count; i++) {
                if (_replicatedVars[i].IsDirty()) {
                    writer.WritePackedInt32(i);
                    _replicatedVars[i].WriteDelta(writer);
                }
            }
        }

        public void ReadValues(NetworkReader reader) {
            var count = reader.ReadPackedInt32();
            if (count != _replicatedVars.Count)
                throw new Exception();

            for (var i = 0; i < _replicatedVars.Count; i++) {
                _replicatedVars[i].Read(reader);
            }
        }

        public void ReadDeltas(NetworkReader reader) {
            // Loop over *any* buffer data
            while (reader.position + 4 < reader.length) {
                var i = reader.ReadPackedInt32();
                _replicatedVars[i].ReadDelta(reader);
            }
        }
    }
}