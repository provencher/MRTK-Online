using System;
using UnityEngine;
using UnityEditor;

namespace Normal.Realtime {
    [CustomEditor(typeof(RealtimeAvatar))]
    public class RealtimeAvatarEditor : Editor {
        private RealtimeAvatar        realtimeAvatar { get { return (RealtimeAvatar)target; } }
        private SerializedProperty      headProperty { get { return serializedObject.FindProperty("_head");      } }
        private SerializedProperty  leftHandProperty { get { return serializedObject.FindProperty("_leftHand");  } }
        private SerializedProperty rightHandProperty { get { return serializedObject.FindProperty("_rightHand"); } }

        public override void OnInspectorGUI() {
            GUILayout.Space(4);

            // Properties
            serializedObject.Update();

                 headProperty.objectReferenceValue = EditorGUILayout.ObjectField("Head",            headProperty.objectReferenceValue, typeof(Transform), true);
             leftHandProperty.objectReferenceValue = EditorGUILayout.ObjectField("Left Hand",   leftHandProperty.objectReferenceValue, typeof(Transform), true);
            rightHandProperty.objectReferenceValue = EditorGUILayout.ObjectField("Right Hand", rightHandProperty.objectReferenceValue, typeof(Transform), true);

            GUILayout.Space(4);

            // Create Avatar Prefab
            GUI.enabled = !Application.isPlaying;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Avatar Prefab"))
                CreateAvatarPrefab();
            GUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();

            GUILayout.Space(4);
        }

        void CreateAvatarPrefab() {
            GameObject gameObject = realtimeAvatar.gameObject;

            //// Root
            // RealtimeView
            RealtimeView rootRealtimeView = AddRealtimeViewComponentIfNeeded(gameObject);

            // RealtimeAvatar
            AddComponentToRealtimeViewIfNeeded(rootRealtimeView, realtimeAvatar);

            // RealtimeTransform
            RealtimeTransform rootRealtimeTransform = AddRealtimeTransformComponentIfNeeded(gameObject, true);
            AddComponentToRealtimeViewIfNeeded(rootRealtimeView, rootRealtimeTransform);


            //// Head
            Transform head = CreateGameObjectForPropertyIfNeeded(headProperty, gameObject.transform, "Head", new Type[]{ typeof(Examples.VoiceScale) });

            // RealtimeView
            RealtimeView headRealtimeView = AddRealtimeViewComponentIfNeeded(head.gameObject);

            // RealtimeTransform
            RealtimeTransform headRealtimeTransform = AddRealtimeTransformComponentIfNeeded(head.gameObject, false);
            AddComponentToRealtimeViewIfNeeded(headRealtimeView, headRealtimeTransform);

            // RealtimeAvatarVoice
            RealtimeAvatarVoice headRealtimeAvatarVoice = head.gameObject.GetComponent<RealtimeAvatarVoice>();
            if (headRealtimeAvatarVoice == null) {
                headRealtimeAvatarVoice = head.gameObject.AddComponent<RealtimeAvatarVoice>();

                // Collapse inspector
                SetComponentInspectorExpanded(headRealtimeAvatarVoice, false);
            }
            AddComponentToRealtimeViewIfNeeded(headRealtimeView, headRealtimeAvatarVoice);


            //// Left Hand
            Transform leftHand = CreateGameObjectForPropertyIfNeeded(leftHandProperty, gameObject.transform, "Left Hand");

            // RealtimeView
            RealtimeView leftHandRealtimeView = AddRealtimeViewComponentIfNeeded(leftHand.gameObject);

            // RealtimeTransform
            RealtimeTransform leftHandRealtimeTransform = AddRealtimeTransformComponentIfNeeded(leftHand.gameObject, false);
            AddComponentToRealtimeViewIfNeeded(leftHandRealtimeView, leftHandRealtimeTransform);


            //// Right Hand
            Transform rightHand = CreateGameObjectForPropertyIfNeeded(rightHandProperty, gameObject.transform, "Right Hand");

            // RealtimeView
            RealtimeView rightHandRealtimeView = AddRealtimeViewComponentIfNeeded(rightHand.gameObject);

            // RealtimeTransform
            RealtimeTransform rightHandRealtimeTransform = AddRealtimeTransformComponentIfNeeded(rightHand.gameObject, false);
            AddComponentToRealtimeViewIfNeeded(rightHandRealtimeView, rightHandRealtimeTransform);
        }

        static RealtimeView AddRealtimeViewComponentIfNeeded(GameObject gameObject) {
            RealtimeView realtimeView = gameObject.GetComponent<RealtimeView>();
            if (realtimeView == null) {
                realtimeView = gameObject.AddComponent<RealtimeView>();

                // Collapse inspector
                SetComponentInspectorExpanded(realtimeView, false);
            }
            return realtimeView;
        }

        static RealtimeTransform AddRealtimeTransformComponentIfNeeded(GameObject gameObject, bool syncScale) {
            // Check for existing RealtimeTransform
            RealtimeTransform realtimeTransform = gameObject.GetComponent<RealtimeTransform>();

            // Create one if needed
            if (realtimeTransform == null) {
                realtimeTransform = gameObject.AddComponent<RealtimeTransform>();

                // Set syncScale
                SerializedObject realtimeTransformSerializedObject = new SerializedObject(realtimeTransform);
                realtimeTransformSerializedObject.Update();
                SerializedProperty syncScaleProperty = realtimeTransformSerializedObject.FindProperty("_syncScale");
                syncScaleProperty.boolValue = syncScale;
                realtimeTransformSerializedObject.ApplyModifiedProperties();

                // Collapse inspector
                SetComponentInspectorExpanded(realtimeTransform, false);
            }

            // Return
            return realtimeTransform;
        }

        static void AddComponentToRealtimeViewIfNeeded(RealtimeView realtimeView, Component component) {
            SerializedObject realtimeViewSerializedObject = new SerializedObject(realtimeView);
            realtimeViewSerializedObject.Update();

            SerializedProperty realtimeViewComponentsProperty = realtimeViewSerializedObject.FindProperty("_components");

            // Check if the component already exists on the RealtimeView
            int numberOfProperties = realtimeViewComponentsProperty.arraySize;
            int largestPropertyIDSeen = 0;
            for (int i = 0; i < numberOfProperties; i++) {
                SerializedProperty realtimeViewComponentProperty = realtimeViewComponentsProperty.GetArrayElementAtIndex(i);
                SerializedProperty realtimeViewComponentComponentIDProperty = realtimeViewComponentProperty.FindPropertyRelative("componentID");
                SerializedProperty realtimeViewComponentComponentProperty   = realtimeViewComponentProperty.FindPropertyRelative("component");

                // Record the property ID
                largestPropertyIDSeen = realtimeViewComponentComponentIDProperty.intValue;

                // We found the component. We're done.
                if (realtimeViewComponentComponentProperty.objectReferenceValue == component)
                    return;
            }
            
            // Component not found, add it.
            int newPropertyIndex = realtimeViewComponentsProperty.arraySize;
            realtimeViewComponentsProperty.InsertArrayElementAtIndex(newPropertyIndex);
            SerializedProperty realtimeViewNewComponentProperty = realtimeViewComponentsProperty.GetArrayElementAtIndex(newPropertyIndex);
            SerializedProperty realtimeViewNewComponentComponentIDProperty = realtimeViewNewComponentProperty.FindPropertyRelative("componentID");
            SerializedProperty realtimeViewNewComponentComponentProperty   = realtimeViewNewComponentProperty.FindPropertyRelative("component");
            realtimeViewNewComponentComponentIDProperty.intValue           = largestPropertyIDSeen+1;
            realtimeViewNewComponentComponentProperty.objectReferenceValue = component;

            realtimeViewSerializedObject.ApplyModifiedProperties();
        }

        static Transform CreateGameObjectForPropertyIfNeeded(SerializedProperty serializedProperty, Transform parent, string gameObjectName, Type[] components = null) {
            Transform transform = serializedProperty.objectReferenceValue as Transform;
            if (transform == null) {
                transform = CreateGameObjectWithCubeGeometry(gameObjectName, parent, components);
                serializedProperty.objectReferenceValue = transform;
            }
            return transform;
        }

        static Transform CreateGameObjectWithCubeGeometry(string name, Transform parent, Type[] components = null) {
            if (components == null)
                components = new Type[0];

            GameObject gameObject = new GameObject(name, components);
            Transform  transform  = gameObject.transform;
            transform.SetParent(parent, false);

            GameObject geometry = GameObject.CreatePrimitive(PrimitiveType.Cube);
            geometry.name = "Geometry";

            Transform  geometryTransform = geometry.transform;
            geometryTransform.SetParent(transform, false);
            geometryTransform.localScale = Vector3.one * 0.1f;

            return transform;
        }

        static void SetComponentInspectorExpanded(Component component, bool expanded) {
            // Set expanded
            UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(component, expanded);

            // Refresh selection
            GameObject gameObject = Selection.activeGameObject;
            Selection.activeGameObject = null;
            Selection.activeGameObject = gameObject;
        }
    }
}
