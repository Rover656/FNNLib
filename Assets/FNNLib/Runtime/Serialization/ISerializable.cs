namespace FNNLib.Serialization {
    public interface ISerializable {
        void Serialize(NetworkWriter writer);
        void DeSerialize(NetworkReader reader);
    }
}