using System.Collections.Generic;
using System.Linq;
using UnityEditor.Callbacks;
using UnityEngine;

// Uses the MLAPI technique for getting instance IDs
// https://github.com/MidLevel/MLAPI/blob/88bdf8b372cee16d49a30c5786de8a151b928b2c/MLAPI-Editor/PostProcessScene.cs

namespace FNNLib.Editor {
    public class ScenePostProcessing {
        [PostProcessScene(int.MaxValue)]
        public static void ProcessScene() {
            // Get all identities
            var sortedObjects = Object.FindObjectsOfType<NetworkIdentity>().ToList();

            // Sort identities
            sortedObjects.Sort((x, y) => {
                                   var xSiblingIndex = x.TraversedSiblingIndex();
                                   var ySiblingIndex = y.TraversedSiblingIndex();

                                   while (xSiblingIndex.Count > 0 && ySiblingIndex.Count > 0) {
                                       if (xSiblingIndex[0] < ySiblingIndex[0])
                                           return -1;

                                       if (xSiblingIndex[0] > ySiblingIndex[0])
                                           return 1;

                                       xSiblingIndex.RemoveAt(0);
                                       ySiblingIndex.RemoveAt(0);
                                   }

                                   return 0;
                               });
            
            for (ulong i = 0; i < (ulong)sortedObjects.Count; i++)
                sortedObjects[(int)i].sceneInstanceID = i;
        }
    }

    internal static class PrefabHelpers {
        internal static List<int> TraversedSiblingIndex(this NetworkIdentity netIdentity) {
            List<int> paths = new List<int>();

            Transform transform = netIdentity.transform;

            while (transform != null) {
                paths.Add(transform.GetSiblingIndex());
                transform = transform.parent;
            }

            paths.Reverse();

            return paths;
        }
    }
}