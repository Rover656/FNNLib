using System;
using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.ReplicatedVar {
    [Serializable]
    public class ReplicatedVar<T> : IReplicatedVar {
        [SerializeField] private T _internalValue = default(T);

        public T value {
            get {
                return _internalValue;
            }
            // TODO: Set with events.
        }

        public bool isDirty = false;

        private NetworkBehaviour _networkBehaviour;

        public bool IsDirty() {
            if (!isDirty) return false;
            // TODO: Other conditions
            return true;
        }

        public void Write(NetworkWriter writer) {
            writer.WritePackedObject(_internalValue);
        }

        public void WriteDelta(NetworkWriter writer) => Write(writer);

        public void Read(NetworkReader reader) {
            // TODO: Events
            _internalValue = reader.ReadPackedObject<T>();
        }

        public void ReadDelta(NetworkReader reader) => Read(reader);

        public void SetNetworkBehaviour(NetworkBehaviour behaviour) {
            _networkBehaviour = behaviour;
        }

        public bool CanClientRead(ulong clientId) {
            // TODO: Config. Right now everyone can
            return true;
        }

        public bool CanClientWrite(ulong clientId) {
            // TODO: Config. Right now owners can
            return clientId == _networkBehaviour.ownerClientID;
        }
    }
}