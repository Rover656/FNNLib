namespace FNNLib.Utilities {
    /// <summary>
    /// FNV-1 Hashing.
    /// https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    ///
    /// Learned about this from MLAPI.
    /// </summary>
    public static class Hashing {
        // Hashing constants
        public const uint FNV_prime32 = 16777619;
        public const uint FNV_offset_basis32 = 2166136261;

        public const ulong FNV_prime64 = 1099511628211;
        public const ulong FNV_offset_basis64 = 14695981039346656037;

        public static ushort GetStableHash16(this string text) {
            var hash32 = text.GetStableHash32();
            
            // XOR folding. Never heard of it until now.
            return (ushort) ((hash32 >> 16) ^ hash32);
        }
        
        public static ushort GetStableHash16(this byte[] bytes) {
            var hash32 = bytes.GetStableHash32();
            
            // XOR folding. Never heard of it until now.
            return (ushort) ((hash32 >> 16) ^ hash32);
        }
    
        public static uint GetStableHash32(this string text) {
            unchecked {
                var hash = FNV_offset_basis32;
                foreach (var c in text) {
                    hash *= FNV_prime32;
                    hash ^= c;
                }
                return hash;
            }
        }
        
        public static uint GetStableHash32(this byte[] bytes) {
            unchecked {
                var hash = FNV_offset_basis32;
                foreach (var b in bytes) {
                    hash *= FNV_prime32;
                    hash ^= b;
                }
                return hash;
            }
        }

        public static ulong GetStableHash64(this string text) {
            unchecked {
                var hash = FNV_offset_basis64;
                foreach (var c in text) {
                    hash *= FNV_prime64;
                    hash ^= c;
                }
                return hash;
            }
        }
        
        public static ulong GetStableHash64(this byte[] bytes) {
            unchecked {
                var hash = FNV_offset_basis64;
                foreach (var b in bytes) {
                    hash *= FNV_prime64;
                    hash ^= b;
                }
                return hash;
            }
        }
    }
}