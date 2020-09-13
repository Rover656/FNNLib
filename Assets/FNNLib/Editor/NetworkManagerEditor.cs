﻿using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace FNNLib.Editor {
    [CustomEditor(typeof(NetworkManager))]
    public class NetworkManagerEditor : UnityEditor.Editor {
        #region NetworkManager

        private NetworkManager _networkManager;

        private SerializedProperty _networkConfigProp;

        #endregion

        #region NetworkConfig

        private SerializedProperty _protocolVersionProp;
        private SerializedProperty _transportProp;
        private SerializedProperty _useSceneManagementProp;
        private SerializedProperty _permittedScenesProp;
        private SerializedProperty _networkedPrefabsProp;
        private SerializedProperty _serverTickRateProp;
        private SerializedProperty _hashSizeProp;

        private ReorderableList _networkableScenesList;
        private ReorderableList _networkedPrefabsList;

        #endregion

        #region GUI Initialization

        private void Init() {
            _networkManager = target as NetworkManager;

            // NetworkManager properties
            _networkConfigProp = serializedObject.FindProperty("networkConfig");

            // NetworkConfig properties
            _protocolVersionProp = _networkConfigProp.FindPropertyRelative("protocolVersion");
            _transportProp = _networkConfigProp.FindPropertyRelative("transport");
            _useSceneManagementProp = _networkConfigProp.FindPropertyRelative("useSceneManagement");
            _serverTickRateProp = _networkConfigProp.FindPropertyRelative("serverTickRate");
            _hashSizeProp = _networkConfigProp.FindPropertyRelative("hashSize");
        }

        private void OnEnable() {
            _networkableScenesList = new ReorderableList(serializedObject,
                                                       serializedObject.FindProperty("networkConfig")
                                                                       .FindPropertyRelative("networkableScenes"), true,
                                                       true, true, true);
            _networkableScenesList.elementHeight = 40;
            _networkableScenesList.drawElementCallback = (rect, index, isActive, isFocused) => {
                                                           var element = _networkableScenesList
                                                              .serializedProperty.GetArrayElementAtIndex(index);
                                                           var firstLabelWidth = 85;
                                                           var padding = 20;
                                                           var secondRow = 20;

                                                           var name = element.FindPropertyRelative("sceneName");
                                                           var packingData =
                                                               element.FindPropertyRelative("scenePackingData");

                                                           EditorGUI
                                                              .LabelField(new Rect(rect.x, rect.y, firstLabelWidth, EditorGUIUtility.singleLineHeight),
                                                                          "Name");
                                                           EditorGUI.PropertyField(new Rect(rect.x + firstLabelWidth,
                                                                   rect.y,
                                                                   rect.width - firstLabelWidth - padding,
                                                                   EditorGUIUtility.singleLineHeight),
                                                               name, GUIContent.none);
                                                           
                                                           EditorGUI
                                                              .LabelField(new Rect(rect.x, rect.y + secondRow, firstLabelWidth, EditorGUIUtility.singleLineHeight),
                                                                          "Packing Data");
                                                           EditorGUI.PropertyField(new Rect(rect.x + firstLabelWidth,
                                                                   rect.y + secondRow,
                                                                   rect.width - firstLabelWidth - padding,
                                                                   EditorGUIUtility.singleLineHeight),
                                                               packingData, GUIContent.none);
                                                       };

            _networkableScenesList.drawHeaderCallback = (rect) => {
                                                          EditorGUI.LabelField(rect, "Networkable Scenes");
                                                      };
            _networkedPrefabsList = new ReorderableList(serializedObject,
                                                       serializedObject.FindProperty("networkConfig")
                                                                       .FindPropertyRelative("networkedPrefabs"), true,
                                                       true, true, true);
            _networkedPrefabsList.drawElementCallback = (rect, index, isActive, isFocused) => {
                                                            var element = _networkedPrefabsList
                                                                         .serializedProperty.GetArrayElementAtIndex(index);
                                                            EditorGUI.PropertyField(new Rect(rect.x,
                                                                    rect.y,
                                                                    rect.width,
                                                                    EditorGUIUtility.singleLineHeight),
                                                                element, GUIContent.none);
                                                        };

            _networkedPrefabsList.drawHeaderCallback = (rect) => {
                                                           EditorGUI.LabelField(rect, "Networked Prefabs");
                                                       };
        }

        #endregion

        #region GUI Rendering

        public override void OnInspectorGUI() {
            Init();

            if (!_networkManager.isServer && !_networkManager.isClient) {
                serializedObject.Update();

                EditorGUILayout.LabelField("General", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_protocolVersionProp);
                EditorGUILayout.PropertyField(_transportProp);

                if (_transportProp.objectReferenceValue == null) {
                    EditorGUILayout.HelpBox("You must select a transport before using FNNLib!", MessageType.Warning);
                }

                EditorGUILayout.LabelField("Scene Management", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_useSceneManagementProp);

                using (new EditorGUI.DisabledScope(!_networkManager.networkConfig.useSceneManagement)) {
                    _networkableScenesList.DoLayoutList();
                }
                
                EditorGUILayout.LabelField("Spawning", EditorStyles.boldLabel);
                _networkedPrefabsList.DoLayoutList();
                
                EditorGUILayout.LabelField("Client", EditorStyles.boldLabel);

                EditorGUILayout.LabelField("Server", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_serverTickRateProp);
                
                EditorGUILayout.LabelField("Hashing", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_hashSizeProp);

                serializedObject.ApplyModifiedProperties();
            }
        }

        #endregion
    }
}