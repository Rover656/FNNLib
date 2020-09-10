// From https://github.com/vis2k/Mirror/blob/aae2e7d871fcd43436fb927d572fff9311fd9911/Assets/Mirror/Runtime/StringHash.cs

namespace FNNLib.Utilities {
    public static class StringHash {
        // string.GetHashCode is not guaranteed to be the same on all machines, but
        // we need one that is the same on all machines. simple and stupid:
        public static int GetStableHashCode(this string text) {
            unchecked {
                int hash = 23;
                foreach (char c in text)
                    hash = hash * 31 + c;
                return hash;
            }
        }
    }
}