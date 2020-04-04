using System;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace Normal.Realtime {
    [InitializeOnLoad]
    [CustomEditor(typeof(RealtimeView))]
    public class RealtimeViewEditor : Editor {
        // Class
        private static bool __showAdvancedSettings = false;

        // Instance
        private RealtimeView realtimeView { get { return (RealtimeView)target; } }
        // TODO: Realtime should expose a state enum instead of having to hit the room directly I think. Or at least a bool for if we're connected or not
        private bool isOnline { get { if (realtimeView.realtime == null) return false; if (realtimeView.realtime.room == null) return false; return realtimeView.realtime.room.connectionState == Room.ConnectionState.Ready; } }
        private SerializedProperty realtimeProperty                                    { get { return serializedObject.FindProperty("_realtime"); } }
        private SerializedProperty sceneViewUUIDProperty                               { get { return serializedObject.FindProperty("_sceneViewUUID");                               } }
        private SerializedProperty sceneViewOwnedByCreatingClientDProperty             { get { return serializedObject.FindProperty("_sceneViewOwnedByCreatingClient");              } }
        private SerializedProperty sceneViewPreventOwnershipTakeoverProperty           { get { return serializedObject.FindProperty("_sceneViewPreventOwnershipTakeover");           } }
        private SerializedProperty sceneViewDestroyWhenOwnerOrLastClientLeavesProperty { get { return serializedObject.FindProperty("_sceneViewDestroyWhenOwnerOrLastClientLeaves"); } }

        // GUI
        private ReorderableList _components;
        private ReorderableList _childViews;

        static RealtimeViewEditor() {
#if UNITY_2018_1_OR_NEWER
            EditorApplication.hierarchyChanged       += RealtimeViewConfiguration._HierarchyDidChange;
#else
            EditorApplication.hierarchyWindowChanged += RealtimeViewConfiguration._HierarchyDidChange;
#endif
        }

        private void OnEnable() {
            __showAdvancedSettings = EditorPrefs.GetBool("Normal.RealtimeViewEditor.ShowAdvancedSettings");

            _components = new ReorderableList(serializedObject, serializedObject.FindProperty("_components"));
            _components.displayAdd    = false;
            _components.displayRemove = false;
            _components.draggable     = false;
            _components.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, "Components");
            };
            _components.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                SerializedProperty componentProperty = _components.serializedProperty.GetArrayElementAtIndex(index);
                SerializedProperty componentComponentIDProperty               = componentProperty.FindPropertyRelative("componentID");
                SerializedProperty componentComponentProperty                 = componentProperty.FindPropertyRelative("component");
                SerializedProperty componentComponentIDHasBeenUsedProperty    = componentProperty.FindPropertyRelative("componentIDHasBeenUsed");

                rect.y += 2;
                EditorGUI.BeginDisabledGroup(true);
                if (componentComponentProperty.objectReferenceValue == null && componentComponentIDProperty.boolValue) {
                    // This field has been used before. Show that the component ID is deprecated.
                    EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width - 30, EditorGUIUtility.singleLineHeight), "Deprecated View");
                    EditorGUI.LabelField(new Rect(rect.x + (rect.width - 24), rect.y, 20, EditorGUIUtility.singleLineHeight), "" + componentComponentIDProperty.intValue);
                } else {
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width - 30, EditorGUIUtility.singleLineHeight), componentComponentProperty, GUIContent.none);
                    EditorGUI.PropertyField(new Rect(rect.x + (rect.width - 24), rect.y, 20, EditorGUIUtility.singleLineHeight), componentComponentIDProperty, GUIContent.none);
                }
                EditorGUI.EndDisabledGroup();

                if (!componentComponentIDHasBeenUsedProperty.boolValue && componentComponentProperty.objectReferenceValue != null) {
                    componentComponentIDHasBeenUsedProperty.boolValue = true;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            };
            _components.onAddCallback = (ReorderableList list) => {
                int numberOfComponents = list.serializedProperty.arraySize;
                int nextAvailableComponentID = 1;
                if (numberOfComponents > 0) {
                    int largestComponentID = 0;
                    for (int i = 0; i < numberOfComponents; i++) {
                        SerializedProperty component = list.serializedProperty.GetArrayElementAtIndex(i);
                        int componentID = component.FindPropertyRelative("componentID").intValue;
                        if (componentID > largestComponentID)
                            largestComponentID = componentID;
                    }
                    nextAvailableComponentID = largestComponentID + 1;
                }
                list.serializedProperty.arraySize = numberOfComponents + 1;
                list.index = numberOfComponents;

                SerializedProperty newComponent = list.serializedProperty.GetArrayElementAtIndex(numberOfComponents);
                newComponent.FindPropertyRelative("componentID").intValue = nextAvailableComponentID;
                newComponent.FindPropertyRelative("component").objectReferenceValue = null;
            };
            
            _childViews = new ReorderableList(serializedObject, serializedObject.FindProperty("_childViews"));
            _childViews.displayAdd    = false;
            _childViews.displayRemove = false;
            _childViews.draggable     = false;
            _childViews.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, "Children");
            };
            _childViews.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                SerializedProperty viewProperty = _childViews.serializedProperty.GetArrayElementAtIndex(index);
                SerializedProperty viewViewIDProperty               = viewProperty.FindPropertyRelative("viewID");
                SerializedProperty viewViewProperty                 = viewProperty.FindPropertyRelative("view");
                SerializedProperty viewViewIDHasBeenUsedProperty    = viewProperty.FindPropertyRelative("viewIDHasBeenUsed");

                rect.y += 2;
                EditorGUI.BeginDisabledGroup(true);
                if (viewViewProperty.objectReferenceValue == null && viewViewIDHasBeenUsedProperty.boolValue) {
                    // This field has been used before. Show that the view ID is deprecated.
                    EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width - 30, EditorGUIUtility.singleLineHeight), "Deprecated View");
                    EditorGUI.LabelField(new Rect(rect.x + (rect.width - 24), rect.y, 20, EditorGUIUtility.singleLineHeight), "" + viewViewIDProperty.intValue);
                } else {
                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width - 30, EditorGUIUtility.singleLineHeight), viewViewProperty, GUIContent.none);
                    EditorGUI.PropertyField(new Rect(rect.x + (rect.width - 24), rect.y, 20, EditorGUIUtility.singleLineHeight), viewViewIDProperty, GUIContent.none);
                }
                EditorGUI.EndDisabledGroup();

                if (!viewViewIDHasBeenUsedProperty.boolValue && viewViewProperty.objectReferenceValue != null) {
                    viewViewIDHasBeenUsedProperty.boolValue = true;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            };
            _childViews.onAddCallback = (ReorderableList list) => {
                int numberOfChildViews = list.serializedProperty.arraySize;
                int nextAvailableChildViewID = 1;
                if (numberOfChildViews > 0) {
                    int largestChildViewID = 0;
                    for (int i = 0; i < numberOfChildViews; i++) {
                        SerializedProperty view = list.serializedProperty.GetArrayElementAtIndex(i);
                        int viewID = view.FindPropertyRelative("viewID").intValue;
                        if (viewID > largestChildViewID)
                            largestChildViewID = viewID;
                    }
                    nextAvailableChildViewID = largestChildViewID + 1;
                }
                list.serializedProperty.arraySize = numberOfChildViews + 1;
                list.index = numberOfChildViews;

                SerializedProperty newView = list.serializedProperty.GetArrayElementAtIndex(numberOfChildViews);
                newView.FindPropertyRelative("viewID").intValue = nextAvailableChildViewID;
                newView.FindPropertyRelative("view").objectReferenceValue = null;
            };
        }

        public override void OnInspectorGUI() {
            GUILayout.Space(12);

            // Properties Start
            GUI.enabled = !Application.isPlaying;
            serializedObject.Update();

            // Scene View
            bool   isRootSceneView = realtimeView.isRootSceneView;
            byte[] sceneViewUUID = RealtimeViewConfiguration.GetSceneViewUUIDAsByteArray(sceneViewUUIDProperty);
            if (isRootSceneView) {
                // UUID
                string sceneViewUUIDString = "";
                if (sceneViewUUID.Length == 16) {
                    sceneViewUUIDString = new Guid(sceneViewUUID).ToString();
                } else {
                    StringBuilder sceneViewUUIDHexString = new StringBuilder(sceneViewUUID.Length * 2);
                    foreach (byte value in sceneViewUUID)
                        sceneViewUUIDHexString.AppendFormat("{0:x2}", value);
                    sceneViewUUIDString = sceneViewUUIDHexString.ToString();
                }

                GUILayout.Label("Scene View UUID: " + sceneViewUUIDString);

                GUILayout.Space(4);
            }


            // Components
            _components.DoLayoutList();

            GUILayout.Space(4);

            // Child Views
            _childViews.DoLayoutList();
            
            // Right now all advanced settings are scene view specific. If that changes, remove this if statement.
            if (isRootSceneView) {
                // Advanced Settings
                GUIStyle foldoutStyle = EditorStyles.foldout;
                FontStyle previousFoldoutFontStyle = foldoutStyle.fontStyle;
                //foldoutStyle.fontStyle = FontStyle.Bold;
                __showAdvancedSettings = EditorGUILayout.Foldout(__showAdvancedSettings, "Advanced Settings", foldoutStyle);
                foldoutStyle.fontStyle = previousFoldoutFontStyle;
                
                EditorPrefs.SetBool("Normal.RealtimeViewEditor.ShowAdvancedSettings", __showAdvancedSettings);
                
                if (__showAdvancedSettings) {
                    if (isRootSceneView) {
                        // Realtime
                        realtimeProperty.objectReferenceValue = EditorGUILayout.ObjectField("Realtime Instance", realtimeProperty.objectReferenceValue, typeof(Realtime), true);
                        
                        // Ownership & lifetime
                        sceneViewOwnedByCreatingClientDProperty.boolValue             = EditorGUILayout.ToggleLeft("Owned By Creating Client", sceneViewOwnedByCreatingClientDProperty.boolValue);
                        sceneViewPreventOwnershipTakeoverProperty.boolValue           = EditorGUILayout.ToggleLeft("Prevent Ownership Takeover", sceneViewPreventOwnershipTakeoverProperty.boolValue);
                        sceneViewDestroyWhenOwnerOrLastClientLeavesProperty.boolValue = EditorGUILayout.ToggleLeft("Destroy When Owner Or Last Client Leaves", sceneViewDestroyWhenOwnerOrLastClientLeavesProperty.boolValue);
                        
                        // Reset UUID
                        if (GUILayout.Button("Reset View UUID")) {
                            if (EditorUtility.DisplayDialog("Reset RealtimeView UUID", "YO!!! This is a dangerous operation! Be careful!\n\nThis RealtimeView will no longer be able to retrieve persistent state that was stored under the previous UUID.\n\nAre you sure you want to reset it?", "OK", "Cancel")) {
                                sceneViewUUID = RealtimeViewConfiguration.SetSceneViewUUIDUsingByteArray(sceneViewUUIDProperty, Guid.NewGuid().ToByteArray());

                                // Update the map
                                RealtimeViewConfiguration._UpdateRealtimeViewSceneViewUUID(realtimeView, sceneViewUUID);
                            }
                        }
                    }
                    GUILayout.Space(2);
                }
            }

            // Properties End
            serializedObject.ApplyModifiedProperties();
            GUILayout.Space(2);


            // Ownership
            GUI.enabled = isOnline;
            GUILayout.Label("Owner: " + GetOwner());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Request Ownership"))
                RequestOwnership();
            if (GUILayout.Button("Clear Ownership"))
                ClearOwnership();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Reset
            GUI.enabled = true;
        }

        string GetOwner() {
            if (!isOnline)
                return "Offline";

            int ownerID = realtimeView.model.ownerID;
            // No owner
            if (ownerID == -1)
                return "None";

            // Owned by the local client
            if (ownerID == realtimeView.realtime.clientID)
                return "Local client (clientID: " + ownerID + ")";

            // Owned by a remote client
            return "Remote client (clientID: " + ownerID + ")";
        }
        
        void RequestOwnership() {
            realtimeView.RequestOwnership();
        }

        void ClearOwnership() {
            realtimeView.ClearOwnership();
        }
    }
}
