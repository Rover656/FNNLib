using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FNNLib.Reflection;
using UnityEngine;

namespace FNNLib.Serialization {
    public class SerializationSystem {
        private static Dictionary<Type, FieldInfo[]> _fieldCache = new Dictionary<Type, FieldInfo[]>();

        internal static FieldInfo[] GetTypeFields(Type type) {
            if (_fieldCache.ContainsKey(type))
                return _fieldCache[type];

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             .Where(x => (x.IsPublic ||
                                          x.GetCustomAttributes(typeof(SerializeField), true).Length >
                                          0 && x.GetCustomAttributes(typeof(NonSerializedAttribute), true).Length == 0
                                         ) && CanSerialize(x.FieldType))
                             .OrderBy(x => x.Name).ToArray();
            _fieldCache.Add(type, fields);
            return fields;
        }

        #region Supported Types for Serialization

        private static readonly HashSet<Type> SupportedTypes = new HashSet<Type> {
                                                                   typeof(byte),
                                                                   // typeof(sbyte),
                                                                   typeof(ushort),
                                                                   typeof(short),
                                                                   typeof(uint),
                                                                   typeof(int),
                                                                   typeof(ulong),
                                                                   typeof(long),
                                                                   typeof(float),
                                                                   typeof(double),
                                                                   typeof(decimal),
                                                                   typeof(string),
                                                                   typeof(bool),
                                                                   typeof(Vector2),
                                                                   typeof(Vector3),
                                                                   typeof(Vector4),
                                                                   typeof(Color),
                                                                   typeof(Color32),
                                                                   typeof(Ray),
                                                                   typeof(Quaternion),
                                                                   typeof(char),
                                                                   // typeof(GameObject),
                                                                   // typeof(NetworkIdentity),
                                                                   // typeof(NetworkBehaviour)
                                                               };

        public static bool CanSerialize(Type type) {
            if (type == typeof(Array))
                return CanSerialize(type.GetElementType());
            return type.IsEnum || SupportedTypes.Contains(type) || type.HasInterface(typeof(ISerializable));
        }

        #endregion
    }
}