using System;
using UnityEditor;
using UnityEngine;

namespace FNNLib {
    [InitializeOnLoad]
    public class NetworkAwareExecution
    {
        static NetworkAwareExecution()
        {
            EditorApplication.delayCall += Update;
            EditorApplication.hierarchyChanged += Update;
            EditorApplication.projectChanged += Update;
        }

        static void Update()
        {
            foreach (var comp in UnityEngine.Object.FindObjectsOfType<Component>())
            {
                NetworkAwareAttribute attribute = (NetworkAwareAttribute)Attribute.GetCustomAttribute(comp.GetType(), typeof(NetworkAwareAttribute));
                if (attribute != null)
                {
                    Transform root = comp.transform.root;

                    if (root.GetComponentsInChildren<NetworkIdentity>().Length == 0)
                    {
                        NetworkBehaviour[] behaviours = root.GetComponentsInChildren<NetworkBehaviour>();
                        Transform topMost = behaviours[0].transform;
                        if (behaviours.Length > 1)
                        {
                            Array.Sort(behaviours, byHeight);

                            topMost = behaviours[0].transform;
                            if (getHeight(behaviours[1].transform) == getHeight(topMost))
                            {
                                topMost = topMost.parent;
                            }
                        }
                        Debug.LogWarningFormat("{0} requires the topmost common gameobject to contain NetworkIdentity. Adding to '{1}'...", comp.GetType(), topMost.name);
                        topMost.gameObject.AddComponent<NetworkIdentity>();
                    } else
                    {
                        NetworkIdentity networkIdentity = root.GetComponentInChildren<NetworkIdentity>();
                        if (networkIdentity.gameObject.GetInstanceID() != comp.gameObject.GetInstanceID())
                        {
                            int aHeight = getHeight(comp.transform);
                            int bHeight = getHeight(networkIdentity.transform);
                            if (aHeight < bHeight)
                            {
                                UnityEngine.Object.DestroyImmediate(networkIdentity);
                                comp.gameObject.AddComponent<NetworkIdentity>();
                            }
                            if (aHeight == bHeight)
                            {
                                UnityEngine.Object.DestroyImmediate(networkIdentity);
                                comp.transform.parent.gameObject.AddComponent<NetworkIdentity>();
                            }
                        }
                    }

                    var arr = root.GetComponentsInChildren<NetworkIdentity>();
                    for (int i = 1; i < arr.Length; i++)
                    {
                        Debug.LogWarningFormat("{0} only allows one NetworkIdentity in its transform hierarchy. Removing NetworkIdentity from '{1}'...", comp.GetType(), arr[i].name);
                        UnityEngine.Object.DestroyImmediate(arr[i]);
                    }
                }
            }
        }

        static int byHeight(NetworkBehaviour a, NetworkBehaviour b)
        {
            return getHeight(a.transform).CompareTo(getHeight(b.transform));
        }

        static int getHeight(Transform element)
        {
            int height = 0;

            while(element != null)
            {
                element = element.parent;
                height++;
            }

            return height;
        }
    }
}