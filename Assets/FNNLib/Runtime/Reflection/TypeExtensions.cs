using System;

namespace FNNLib.Reflection {
    internal static class TypeExtensions {
        internal static bool HasInterface(this Type type, Type interfaceType) {
            var interfaces = type.GetInterfaces();
            for (var i = 0; i < interfaces.Length; i++) {
                if (interfaces[i] == interfaceType)
                    return true;
            }

            return false;
        }

        internal static bool IsNullable(this Type type) {
            if (!type.IsValueType) return true;
            return Nullable.GetUnderlyingType(type) != null;
        }
    }
}