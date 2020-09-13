using System.Collections.Generic;

namespace FNNLib.Spawning {
    // TODO: Look at https://github.com/MidLevel/MLAPI/blob/88bdf8b372cee16d49a30c5786de8a151b928b2c/MLAPI-Editor/PostProcessScene.cs#L37
    //       This shows how MLAPI sorts all scene objects to generate unique instance ID's for non-prefab's in a scene.

    public static class SpawnManager {
        /// <summary>
        /// Objects that have been spawned
        /// </summary>
        public static readonly Dictionary<ulong, NetworkIdentity> spawnedObjects;
        
        
    }
}