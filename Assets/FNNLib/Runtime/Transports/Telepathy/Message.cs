// incoming message queue of <connectionId, message>
// (not a HashSet because one connection can have multiple new messages)
// -> a struct to minimize GC
namespace Telepathy
{
    public struct Message
    {
        public readonly int connectionId;
        public readonly EventType eventType;
        public readonly byte[] data;
        public readonly int channel;
        public Message(int connectionId, EventType eventType, byte[] data, int channel)
        {
            this.connectionId = connectionId;
            this.eventType = eventType;
            this.data = data;
            this.channel = channel;
        }
    }

    public struct OutgoingData {
        public int channel;
        public byte[] data;

        public OutgoingData(int channel, byte[] data) {
            this.channel = channel;
            this.data = data;
        }
    }
}
