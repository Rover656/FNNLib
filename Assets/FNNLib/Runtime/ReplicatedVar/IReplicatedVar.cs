using FNNLib.Serialization;

namespace FNNLib.ReplicatedVar {
    public interface IReplicatedVar {
        bool IsDirty();
        void Write(NetworkWriter writer);
        void WriteDelta(NetworkWriter writer);
        void Read(NetworkReader reader);
        void ReadDelta(NetworkReader reader);
        void SetNetworkBehaviour(NetworkBehaviour behaviour);

        bool CanClientRead(ulong clientId);
        bool CanClientWrite(ulong clientId);
    }
}