namespace FNNLib.Transports {
    public enum ChannelType {
        Unreliable,
        UnreliableSequenced,
        Reliable,
        ReliableSequenced,
        ReliableFragmentedSequenced
    }
    
    public static class DefaultChannels {
        public const int Reliable = 0;
        public const int ReliableSequenced = 1;
        public const int Unreliable = 2;
    }
}