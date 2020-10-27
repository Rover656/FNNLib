namespace FNNLib.Serialization {
    /// <summary>
    /// Automatically serializes a classes fields.
    /// If you want something to be automatically serialized, just inherit this class.
    /// </summary>
    public abstract class AutoSerializer : ISerializable {
        /// <summary>
        /// Automatically serialize all serializable fields.
        /// </summary>
        /// <param name="writer"></param>
        public void Serialize(NetworkWriter writer) {
            // Get all class fields
            var fields = SerializationSystem.GetTypeFields(GetType());
            
            // Write each field
            for (var i = 0; i < fields.Length; i++)
                writer.WritePackedObject(fields[i].GetValue(this));
        }

        /// <summary>
        /// Deserialize all fields.
        /// </summary>
        /// <param name="reader"></param>
        public void DeSerialize(NetworkReader reader) {
            // Get all class fields
            var fields = SerializationSystem.GetTypeFields(GetType());
            
            // Read each field
            for (var i = 0; i < fields.Length; i++)
                fields[i].SetValue(this, reader.ReadPackedObject(fields[i].FieldType));
        }
    }
}