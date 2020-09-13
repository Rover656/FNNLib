﻿using FNNLib.Messaging;
using FNNLib.Serialization;
using UnityEngine;

namespace FNNLib.SceneManagement {
    [ClientPacket]
    public class SceneChangePacket : IPacket {
        /// <summary>
        /// The index of the scene to load.
        /// This is the index of the scene in the allowed scenes list.
        /// </summary>
        public int sceneIndex;

        /// <summary>
        /// The scene network ID.
        /// This is used to distinguish between subscenes that are the same.
        /// </summary>
        public uint sceneNetID;

        /// <summary>
        /// The scene offset (for subscenes which will not be based in 0,0 by default).
        /// </summary>
        public Vector3 sceneOffset;
        
        public void Serialize(NetworkWriter writer) {
            writer.WritePackedInt32(sceneIndex);
            writer.WritePackedUInt32(sceneNetID);
            writer.WriteVector3(sceneOffset);
        }

        public void DeSerialize(NetworkReader reader) {
            sceneIndex = reader.ReadPackedInt32();
            sceneNetID = reader.ReadPackedUInt32();
            sceneOffset = reader.ReadVector3();
        }
    }
}