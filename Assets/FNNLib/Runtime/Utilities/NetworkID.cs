using System;
using System.Collections.Generic;
using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.Utilities {
    /// <summary>
    /// PROTOTYPE.
    /// NetworkID System
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class NetworkID<T> : ISerializable, IDisposable {
        public ulong ID => _id;

        [SerializeField]
        private ulong _id;

        private static readonly List<ulong> _reusableIDs = new List<ulong>();
        private static ulong _internalCounter = 0;
        
        public NetworkID(ulong id) {
            _id = id;
        }

        public static NetworkID<T> GetID() {
            if (_reusableIDs.Count > 0) {
                var id = new NetworkID<T>(_reusableIDs[0]);
                _reusableIDs.RemoveAt(0);
                return id;
            }
            return new NetworkID<T>(_internalCounter++);
        }
        
        public void Serialize(NetworkWriter writer) {
            /*
             * We don't implement type parameter checking for the sole reason of bandwidth.
             * Its on the developer to maintain type checking on their end.
             */
            writer.WritePackedUInt64(_id);
        }

        public void DeSerialize(NetworkReader reader) {
            _id = reader.ReadPackedUInt64();
        }

        public void Dispose() {
            _reusableIDs.Add(_id);
        }
    }
}