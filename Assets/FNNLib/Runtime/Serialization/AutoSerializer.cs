namespace FNNLib.Serialization {
    /// <summary>
    /// Automatically serializes a classes fields.
    /// </summary>
    public abstract class AutoSerializer : ISerializable {
        public void Serialize(NetworkWriter writer) {
            // Get all class fields
            var fields = SerializationSystem.GetTypeFields(GetType());
            
            // Write each field
            for (var i = 0; i < fields.Length; i++)
                writer.WritePackedObject(fields[i].GetValue(this));
        }

        public void DeSerialize(NetworkReader reader) {
            // Get all class fields
            var fields = SerializationSystem.GetTypeFields(GetType());
            
            // Read each field
            for (var i = 0; i < fields.Length; i++)
                fields[i].SetValue(this, reader.ReadPackedObject(fields[i].FieldType));
        }
    }
}