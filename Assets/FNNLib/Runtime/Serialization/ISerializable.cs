namespace FNNLib.Serialization {
    // TODO: https://app.gitkraken.com/glo/view/card/70a90133d59f494ea80c48734de28ed7
    public interface ISerializable {
        void Serialize(NetworkWriter writer);
        void DeSerialize(NetworkReader reader);
    }
}