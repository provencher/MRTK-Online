using System;
using UnityEngine;
using UnityEditor;

namespace Normal.Realtime {
    [CustomEditor(typeof(RealtimeAvatarManager))]
    public class RealtimeAvatarManagerEditor : Editor {
        private RealtimeAvatarManager realtimeAvatarManager { get { return (RealtimeAvatarManager)target; } }
        private bool _showLocalPlayerProperties = false;
        private SerializedProperty localAvatarPrefabProperty { get { return    serializedObject.FindProperty("_localAvatarPrefab"); } }
        private SerializedProperty       localPlayerProperty { get { return    serializedObject.FindProperty("_localPlayer");       } }
        private SerializedProperty              rootProperty { get { return localPlayerProperty.FindPropertyRelative("root");       } }
        private SerializedProperty              headProperty { get { return localPlayerProperty.FindPropertyRelative("head");       } }
        private SerializedProperty          leftHandProperty { get { return localPlayerProperty.FindPropertyRelative("leftHand");   } }
        private SerializedProperty         rightHandProperty { get { return localPlayerProperty.FindPropertyRelative("rightHand");  } }

        void OnEnable() {
            _showLocalPlayerProperties = EditorPrefs.GetBool("Normal.RealtimeAvatarManagerEditor.ShowLocalPlayerProperties");
        }

        public override void OnInspectorGUI() {
            GUILayout.Space(4);

            // Properties
            serializedObject.Update();

            // Avatar Prefab
            localAvatarPrefabProperty.objectReferenceValue = EditorGUILayout.ObjectField("Local Avatar Prefab", localAvatarPrefabProperty.objectReferenceValue, typeof(GameObject), true);

            // Local Player
            _showLocalPlayerProperties = EditorGUILayout.Foldout(_showLocalPlayerProperties, "Local Player", EditorStyles.foldout);

            EditorPrefs.SetBool("Normal.RealtimeAvatarManagerEditor.ShowLocalPlayerProperties", _showLocalPlayerProperties);

            if (_showLocalPlayerProperties) {
                     rootProperty.objectReferenceValue = EditorGUILayout.ObjectField("    Root",            rootProperty.objectReferenceValue, typeof(Transform), true);
                     headProperty.objectReferenceValue = EditorGUILayout.ObjectField("    Head",            headProperty.objectReferenceValue, typeof(Transform), true);
                 leftHandProperty.objectReferenceValue = EditorGUILayout.ObjectField("    Left Hand",   leftHandProperty.objectReferenceValue, typeof(Transform), true);
                rightHandProperty.objectReferenceValue = EditorGUILayout.ObjectField("    Right Hand", rightHandProperty.objectReferenceValue, typeof(Transform), true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
