#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace Normal.Realtime {
    public class RealtimeViewConfiguration {
        // RealtimeView Management
        private static Dictionary<RealtimeView, byte[]> _rootSceneViewUUIDMap = new Dictionary<RealtimeView, byte[]>();

        public static void _UpdateRealtimeViewSceneViewUUID(RealtimeView realtimeView, byte[] sceneViewUUID) {
            _rootSceneViewUUIDMap[realtimeView] = sceneViewUUID;
        }

        public static byte[] GetSceneViewUUIDAsByteArray(SerializedProperty property) {
            byte[] sceneViewUUID = new byte[property.arraySize];
            for (int i = 0; i < property.arraySize; i++) {
                sceneViewUUID[i] = (byte)property.GetArrayElementAtIndex(i).intValue;
            }
            return sceneViewUUID;
        }

        public static byte[] SetSceneViewUUIDUsingByteArray(SerializedProperty property, byte[] sceneViewUUID) {
            property.ClearArray();
            for (int i = 0; i < sceneViewUUID.Length; i++) {
                property.InsertArrayElementAtIndex(i);
                property.GetArrayElementAtIndex(i).intValue = sceneViewUUID[i];
            }
            return sceneViewUUID;
        }

        public static void _HierarchyDidChange() {
            if (Application.isPlaying)
                return;
            
            GameObject[] sceneRootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            Realtime[]   realtimeInstances    = sceneRootGameObjects.SelectMany(i => i.GetComponentsInChildren<Realtime>()).ToArray();
            bool didWarnAboutRealtimeInstancesBeingTooFewOrTooMany = false;

            // Automatically configure scene RealtimeViews
            RealtimeView[] realtimeViews = Resources.FindObjectsOfTypeAll<RealtimeView>();
            foreach (RealtimeView realtimeView in realtimeViews) {
                if (realtimeView == null)
                    continue;
                
                ConfigureRealtimeView(realtimeView, realtimeInstances, ref didWarnAboutRealtimeInstancesBeingTooFewOrTooMany);
            }
        }

        public static void _ConfigureRealtimeView(RealtimeView realtimeView) {
            GameObject[] sceneRootGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            Realtime[]   realtimeInstances    = sceneRootGameObjects.SelectMany(i => i.GetComponentsInChildren<Realtime>()).ToArray();
            bool didWarnAboutRealtimeInstancesBeingTooFewOrTooMany = false;
            ConfigureRealtimeView(realtimeView, realtimeInstances, ref didWarnAboutRealtimeInstancesBeingTooFewOrTooMany);
        }

        private static void ConfigureRealtimeView(RealtimeView realtimeView, Realtime[] realtimeInstances, ref bool didWarnAboutRealtimeInstancesBeingTooFewOrTooMany) {
            SerializedObject realtimeViewSerializedObject = new SerializedObject(realtimeView);
            realtimeViewSerializedObject.Update();
            SerializedProperty         realtimeProperty = realtimeViewSerializedObject.FindProperty("_realtime");
            SerializedProperty    sceneViewUUIDProperty = realtimeViewSerializedObject.FindProperty("_sceneViewUUID");
            SerializedProperty isRootPrefabViewProperty = realtimeViewSerializedObject.FindProperty("_isRootPrefabView");

            // Realtime Instance
            Realtime realtime = realtimeProperty.objectReferenceValue as Realtime;
            bool prefab = EditorUtility.IsPersistent(realtimeView.gameObject);
            if (prefab) {
                if (realtime != null) {
                    realtimeProperty.objectReferenceValue = null;
                }
            } else {
                if (realtime == null) {
                    if (realtimeInstances.Length == 1) {
                        realtimeProperty.objectReferenceValue = realtimeInstances[0];
                    } else if (!didWarnAboutRealtimeInstancesBeingTooFewOrTooMany) {
                        if (realtimeInstances.Length == 0) {
                            Debug.LogWarning("RealtimeView: No instances of Realtime exist in the scene. Make sure to create an instance of Realtime otherwise this scene view will not work!");
                        } else if (realtimeInstances.Length > 1) {
                            Debug.LogWarning("RealtimeView: There are multiple instances of Realtime in the scene. If you plan to use this as a scene view, wire up a reference to Realtime manually under the Advanced Settings panel on the RealtimeView.");
                        }
                        didWarnAboutRealtimeInstancesBeingTooFewOrTooMany = true;
                    }
                }
            }

            // Add realtime components
            RealtimeComponent[] components = realtimeView.GetComponents<RealtimeComponent>();
            foreach (RealtimeComponent component in components) {
                AddComponentToViewListIfNeeded(realtimeView, component);
            }

            // Add to parent RealtimeView if this is a child view
            bool   isRoot = AddChildViewToParentViewIfNeeded(realtimeView, realtimeViewSerializedObject);
            byte[] sceneViewUUID = GetSceneViewUUIDAsByteArray(sceneViewUUIDProperty);

            if (isRoot && !prefab) {
                // Root scene view

                // Make sure this root scene view exists in our map. If it does, verify the UUID is set properly.
                byte[] previouslyAssignedUUID;
                if (_rootSceneViewUUIDMap.TryGetValue(realtimeView, out previouslyAssignedUUID)) {
                    // If previously assigned UUID doesn't match, reset it. This can happen when clicking Apply on a prefab.
                    // This is because the UUID gets stored on the prefab and cleared on the scene view because it inherits the value from the prefab.
                    // Then this script comes in and clears the UUID on the prefab, which then means the scene view will have no UUID because it's inheriting
                    // from the prefab. We'll detect that here and set it back on the scene view.
                    if (!previouslyAssignedUUID.SequenceEqual(sceneViewUUID)) {
                        // Reset scene view UUID
                        sceneViewUUID = SetSceneViewUUIDUsingByteArray(sceneViewUUIDProperty, previouslyAssignedUUID);
                    }
                } else {
                    // Set scene UUID if needed
                    if (sceneViewUUID == null || sceneViewUUID.Length == 0) {
                        sceneViewUUID = SetSceneViewUUIDUsingByteArray(sceneViewUUIDProperty, Guid.NewGuid().ToByteArray());
                    } else {
                        // If this view doesn't exist in the UUID map, but it has a scene view UUID, it's possible it's been copy & pasted. Check the map to see if another scene view has the same UUID. If it does, reset it and log a warning.
                        foreach (KeyValuePair<RealtimeView, byte[]> viewUUIDPair in _rootSceneViewUUIDMap) {
                            RealtimeView view     = viewUUIDPair.Key;
                            byte[]       viewUUID = viewUUIDPair.Value;
                            if (sceneViewUUID.SequenceEqual(viewUUID) && realtimeView != view && realtimeView.gameObject.scene == view.gameObject.scene) {
                                // If we enter this block, it means there's already a realtime view with this UUID loaded, /and/ it exists in the same scene.
                                // As far as I know, the only way for that to happen is to copy & paste a root scene realtime view. In that case, I think it's ok to reset
                                // the UUID on the copy. And since the original is going to be the one that already exists in the map, that means this one is the copy.
                                // For root scene views that have the same UUID as a view in another scene, we'll log an error below. That can happen when a scene is saved
                                // as a copy, and the copy is additively loaded. In that case, we don't know which view the developer will want to keep so we log the error.
                                Debug.LogWarning("Realtime: Found a RealtimeView in scene with a duplicate UUID. Resetting the UUID on the copy.");
                                sceneViewUUID = SetSceneViewUUIDUsingByteArray(sceneViewUUIDProperty, Guid.NewGuid().ToByteArray());
                                break;
                            }
                        }
                    }

                    // Add to the map
                    _rootSceneViewUUIDMap[realtimeView] = sceneViewUUID;
                }
            } else {
                // Not root scene view

                // Clear scene UUID
                if (sceneViewUUID == null || sceneViewUUID.Length != 0) {
                    // Clear the UUID
                    sceneViewUUID = SetSceneViewUUIDUsingByteArray(sceneViewUUIDProperty, new byte[0]);

                    // Remove from map
                    _rootSceneViewUUIDMap.Remove(realtimeView);
                }
            }

            if (isRoot && prefab) {
                // Root prefab view

                // Set isRootPrefabView property
                if (!isRootPrefabViewProperty.boolValue)
                    isRootPrefabViewProperty.boolValue = true;
            } else {
                // Not root prefab view

                // Clear isRootPrefabView property
                if (isRootPrefabViewProperty.boolValue)
                    isRootPrefabViewProperty.boolValue = false;
            }

            realtimeViewSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddComponentToViewListIfNeeded(RealtimeView view, RealtimeComponent componentToAdd) {
            // Add to view's components list.
            SerializedObject viewSerializedObject = new SerializedObject(view);
            viewSerializedObject.Update();
            SerializedProperty componentsProperty = viewSerializedObject.FindProperty("_components");
            int numberOfComponents = componentsProperty.arraySize;

            // Check for component in the list
            int largestComponentID = 0;
            for (int i = 0; i < numberOfComponents; i++) {
                SerializedProperty componentProperty            = componentsProperty.GetArrayElementAtIndex(i);
                SerializedProperty componentComponentIDProperty = componentProperty.FindPropertyRelative("componentID");
                SerializedProperty componentComponentProperty   = componentProperty.FindPropertyRelative("component");
                MonoBehaviour      component                    = componentComponentProperty.objectReferenceValue as MonoBehaviour;

                // If the component exists, we're done.
                if (component == componentToAdd)
                    return;
                
                int componentID = componentComponentIDProperty.intValue;
                if (componentID > largestComponentID)
                    largestComponentID = componentID;
            }

            // Not found. Add to list.
            int componentIndex = numberOfComponents;
            int nextAvailableComponentID = largestComponentID + 1;
            componentsProperty.InsertArrayElementAtIndex(componentIndex);
            SerializedProperty newComponentProperty = componentsProperty.GetArrayElementAtIndex(componentIndex);
            newComponentProperty.FindPropertyRelative("componentID").intValue             = nextAvailableComponentID;
            newComponentProperty.FindPropertyRelative("component").objectReferenceValue   = componentToAdd;
            newComponentProperty.FindPropertyRelative("componentIDHasBeenUsed").boolValue = true;

            viewSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool AddChildViewToParentViewIfNeeded(RealtimeView childView, SerializedObject childViewSerializedObject) {
            if (childView == null) {
                Debug.LogError("Attempting to add null child view to parent view. This is a bug! Bailing...");
                return false;
            }

            // Recursively trace up to the parent
            RealtimeView parentView = FindParentRealtimeViewForTransform(childView.transform);

            // Remove from previous parentView and update _parentView property.
            SerializedProperty parentViewSerializedProperty = childViewSerializedObject.FindProperty("_parentView");
            RealtimeView oldParentView = parentViewSerializedProperty.objectReferenceValue as RealtimeView;
            if (parentView != oldParentView) {
                // We're going to change the parent below. Remove the child view from the old parent view's child view list.
                if (oldParentView != null)
                    RemoveChildViewFromParentViewList(oldParentView, childView);

                // Set parent property on child view
                parentViewSerializedProperty.objectReferenceValue = parentView;
            }
            
            // No parent found, this is a root view.
            if (parentView == null)
                return true;

            // Add to parent view's childViews list.
            SerializedObject parentViewSerializedObject = new SerializedObject(parentView);
            parentViewSerializedObject.Update();
            SerializedProperty childViewsProperty = parentViewSerializedObject.FindProperty("_childViews");
            int numberOfChildViews = childViewsProperty.arraySize;

            // Check for child view in the list
            int largestViewID = 0;
            for (int i = 0; i < numberOfChildViews; i++) {
                SerializedProperty childViewProperty                     = childViewsProperty.GetArrayElementAtIndex(i);
                SerializedProperty childViewViewIDProperty               = childViewProperty.FindPropertyRelative("viewID");
                SerializedProperty childViewViewProperty                 = childViewProperty.FindPropertyRelative("view");
                SerializedProperty childViewViewToUseIfMovedBackProperty = childViewProperty.FindPropertyRelative("viewToUseIfMovedBack");
                RealtimeView view                 = childViewViewProperty.objectReferenceValue as RealtimeView;
                RealtimeView viewToUseIfMovedBack = childViewViewToUseIfMovedBackProperty.objectReferenceValue as RealtimeView;

                // If the view exists, we're done.
                if (view == childView)
                    return false;
                
                // If the view has been assigned to this property before, re-assign it and we're done.
                if (viewToUseIfMovedBack == childView) {
                    childViewViewProperty.objectReferenceValue = childView;
                    parentViewSerializedProperty.objectReferenceValue = parentView;
                    parentViewSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                    return false;
                }
                
                int viewID = childViewViewIDProperty.intValue;
                if (viewID > largestViewID)
                    largestViewID = viewID;
            }

            // Not found. Add to list.
            int viewIndex = numberOfChildViews;
            int nextAvailableViewID = largestViewID + 1;
            childViewsProperty.InsertArrayElementAtIndex(viewIndex);
            SerializedProperty newChildViewProperty = childViewsProperty.GetArrayElementAtIndex(viewIndex);
            newChildViewProperty.FindPropertyRelative("viewID").intValue                           = nextAvailableViewID;
            newChildViewProperty.FindPropertyRelative("view").objectReferenceValue                 = childView;
            newChildViewProperty.FindPropertyRelative("viewToUseIfMovedBack").objectReferenceValue = childView;
            newChildViewProperty.FindPropertyRelative("viewIDHasBeenUsed").boolValue               = true;
            parentViewSerializedObject.ApplyModifiedPropertiesWithoutUndo();

            return false;
        }

        private static RealtimeView FindParentRealtimeViewForTransform(Transform realtimeViewTransform) {
            Transform parent = realtimeViewTransform.parent;
            if (parent == null)
                return null;
            RealtimeView realtimeView = parent.GetComponent<RealtimeView>();
            if (realtimeView != null)
                return realtimeView;
            return FindParentRealtimeViewForTransform(parent);
        }

        private static void RemoveChildViewFromParentViewList(RealtimeView parentView, RealtimeView childView) {
            // Remove from parent view's childViews list.
            SerializedObject parentViewSerializedObject = new SerializedObject(parentView);
            parentViewSerializedObject.Update();
            SerializedProperty childViewsProperty = parentViewSerializedObject.FindProperty("_childViews");
            int numberOfChildViews = childViewsProperty.arraySize;

            // Check for child view in the list
            for (int i = 0; i < numberOfChildViews; i++) {
                SerializedProperty  childViewProperty = childViewsProperty.GetArrayElementAtIndex(i);
                SerializedProperty  childViewViewProperty = childViewProperty.FindPropertyRelative("view");
                SerializedProperty  childViewViewToUseIfMovedBackProperty = childViewProperty.FindPropertyRelative("viewToUseIfMovedBack");
                RealtimeView view = childViewViewProperty.objectReferenceValue as RealtimeView;

                // If the view exists, clear it out.
                if (view == childView) {
                    // Clear view property
                    childViewViewProperty.objectReferenceValue = null;

                    // Store a reference so if the view is moved back, it gets assigned to the same View ID
                    childViewViewToUseIfMovedBackProperty.objectReferenceValue = childView;
                }
            }
            parentViewSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
