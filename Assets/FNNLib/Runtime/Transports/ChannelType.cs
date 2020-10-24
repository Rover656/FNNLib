using System;

namespace FNNLib.Transports {
    public enum ChannelType {
        Unreliable,
        UnreliableSequenced,
        Reliable,
        ReliableSequenced,
        ReliableFragmentedSequenced
    }
    
    public static class DefaultChannels {
        [Obsolete("Please use NetworkChannel instead. Channel IDs will not be used for much longer.")]
        public const int Reliable = 0;
        [Obsolete("Please use NetworkChannel instead. Channel IDs will not be used for much longer.")]
        public const int ReliableSequenced = 1;
        [Obsolete("Please use NetworkChannel instead. Channel IDs will not be used for much longer.")]
        public const int Unreliable = 2;
    }
}