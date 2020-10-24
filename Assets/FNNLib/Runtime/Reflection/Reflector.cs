using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace FNNLib.Reflection {
    public abstract class Reflector {
        protected static List<MethodInfo> GetMethodsRecursive(Type type, Type typeLimit) {
            var methods = new List<MethodInfo>();

            while (type != null && type != typeLimit) {
                methods.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                                 BindingFlags.DeclaredOnly));
                type = type.BaseType;
            }

            return methods;
        }

        protected static List<FieldInfo> GetFields(Type type, Type typeLimit) {
            // TODO: Cache system
            return GetFieldsRecursive(type, typeLimit);
        }

        private static List<FieldInfo> GetFieldsRecursive(Type type, Type typeLimit, List<FieldInfo> list = null) {
            if (list == null) {
                list = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).ToList();
            } else {
                list.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            }

            if (type.BaseType != null && type.BaseType != typeLimit) {
                return GetFieldsRecursive(type, typeLimit, list);
            } else {
                return list.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
            }
        }
    }
}