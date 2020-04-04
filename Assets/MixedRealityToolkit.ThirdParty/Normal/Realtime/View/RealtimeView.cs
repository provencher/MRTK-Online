using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;
using Normal.Realtime;
using Normal.Realtime.Serialization;
using Normal.Utility;

namespace Normal.Realtime {
    [DisallowMultipleComponent]
    public class RealtimeView : MonoBehaviour {
        [SerializeField]
        private Realtime _realtime;
        public  Realtime  realtime { get { return _realtime; } }
    
        public  int ownerID       { get { return _model.ownerID;       } set { _model.ownerID       = value; } }
        public uint lifetimeFlags { get { return _model.lifetimeFlags; } set { _model.lifetimeFlags = value; } }
    
        public bool isOwnedLocally { get { return _model.ownerID == realtime.clientID; } }
        public bool isOwnedByWorld { get { return _model.ownerID == -1; } }

#pragma warning disable 0649 // Disable variable is never assigned to warning.
        [SerializeField]
        private byte[] _sceneViewUUID = { };
        public  byte[]  sceneViewUUID { get { return _sceneViewUUID; } }
        public  bool isRootSceneView { get { return _sceneViewUUID.Length != 0; } }
        [SerializeField]
        private bool _sceneViewOwnedByCreatingClient = true;
        [SerializeField]
        private bool _sceneViewPreventOwnershipTakeover = false;
        [SerializeField]
        private bool _sceneViewDestroyWhenOwnerOrLastClientLeaves = true;

        [SerializeField]
        private bool _isRootPrefabView;
        public  bool  isRootPrefabView { get { return _isRootPrefabView; } }

        public  bool  isChildView { get { return !isRootSceneView && !isRootPrefabView; } }
    
        private RealtimeViewModel _model;
        public  RealtimeViewModel  model { get { return _model; } set { SetModel(value); } }
    
        [Serializable]
        public class RealtimeViewIDComponentPair {
            [FormerlySerializedAs("propertyID")]
            public int           componentID;
            public MonoBehaviour component;
            public bool          componentIDHasBeenUsed;
        }
        [FormerlySerializedAs("_properties")]
        [SerializeField]
        private RealtimeViewIDComponentPair[] _components;

#if UNITY_EDITOR
        [SerializeField]
        private RealtimeView _parentView;
#endif

        [Serializable]
        public class RealtimeViewChildIDViewPair {
            public int          viewID;
            public RealtimeView view;
            public bool         viewIDHasBeenUsed;
            public RealtimeView viewToUseIfMovedBack; // If a RealtimeView is moved to a child view, then moved back, we use this property to make sure it's assigned to its original viewID.
        }
        [SerializeField]
        private RealtimeViewChildIDViewPair[] _childViews;
#pragma warning restore 0649

        private void Start() {
            // If this is a root scene view, register with Realtime
            if (isRootSceneView) {
                if (_realtime == null) {
                    // This can happen if this RealtimeView is a scene view in a scene that's meant to be additively loaded onto a scene that has a Realtime instance in it. Attempt to auto-detect.
                    if (Realtime.instances.Count == 1) {
                        foreach (Realtime instance in Realtime.instances) {
                            if (instance == null) {
                                Debug.LogError("RealtimeView: Realtime.instances contains a null value. This is a bug!");
                            }
                            _realtime = instance;
                            break;
                        }
                    } else if (Realtime.instances.Count == 0) {
                        Debug.LogError("RealtimeView: Attempting to auto-detect Realtime instance, but none exist in the scene yet. Please make sure there's an instance of Realtime in the scene.");
                    } else if (Realtime.instances.Count > 1) {
                        Debug.LogError("RealtimeView: Attempting to auto-detect Realtime instance, but multiple instances of Realtime exist in the scene. Please wire up a reference to Realtime manually in Advanced Settings on each scene RealtimeView.");
                    }
                }
    
                if (_realtime != null)
                    _realtime._RegisterSceneRealtimeView(this);
            }
        }
    
        private void OnDestroy() {
            // If this is a root scene view, unregister with Realtime
            if (isRootSceneView) {
                if (_realtime != null)
                    _realtime._UnregisterSceneRealtimeView(this);
            }
        }

        private void Reset() {
#if UNITY_EDITOR
            RealtimeViewConfiguration._ConfigureRealtimeView(this);
 #endif
        }

        // Used to populate _realtime for prefab RealtimeViews
        public void _SetRealtime(Realtime realtime) {
            _realtime = realtime;
        }
    
        public RealtimeViewModel _CreateRootSceneViewModel() {
            if (!isRootSceneView) {
                Debug.LogError("RealtimeView: Asked to create root scene view model for view that's not a root scene view. This is a bug!");
            }

            // Create components model
            Dictionary<int, Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>> componentMap = CreateComponentMap();
            RealtimeViewComponentsModel componentsModel = CreateComponentsModel(componentMap);

            // Create child views model
            Dictionary<int, RealtimeView> childViewsMap = CreateChildViewMap();
            RealtimeViewComponentsModel childViewsModel = CreateChildViewsModel(childViewsMap);
    
            // Ownership / lifetime flags
            int  ownerID = _sceneViewOwnedByCreatingClient ? _realtime.clientID : -1;
            uint lifetimeFlags = 0;
    
            if (_sceneViewPreventOwnershipTakeover)
                lifetimeFlags |= (uint)MetaModel.LifetimeFlags.PreventOwnershipTakeover;
    
            if (_sceneViewDestroyWhenOwnerOrLastClientLeaves)
                lifetimeFlags |= (uint)MetaModel.LifetimeFlags.DestroyWhenOwnerOrLastClientLeaves;
    
            // Create RealtimeViewModel
            RealtimeViewModel viewModel = new RealtimeViewModel(_sceneViewUUID, ownerID, lifetimeFlags, componentsModel, childViewsModel);
    
            return viewModel;
        }
    
        public RealtimeViewModel _CreateRootPrefabViewModel(string prefabName, int ownerID, uint lifetimeFlags) {
            if (!isRootPrefabView) {
                Debug.LogError("RealtimeView: Asked to create root prefab view model for view that's not on the root of a prefab. This is a bug!");
            }

            // Create components model
            Dictionary<int, Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>> componentMap = CreateComponentMap();
            RealtimeViewComponentsModel componentsModel = CreateComponentsModel(componentMap);

            // Create child views model
            Dictionary<int, RealtimeView> childViewsMap = CreateChildViewMap();
            RealtimeViewComponentsModel childViewsModel = CreateChildViewsModel(childViewsMap);
    
            // Create RealtimeViewModel
            RealtimeViewModel viewModel = new RealtimeViewModel(prefabName, ownerID, lifetimeFlags, componentsModel, childViewsModel);
    
            return viewModel;
        }

        public RealtimeViewModel _CreateChildViewModel() {
            if (!isChildView) {
                Debug.LogError("RealtimeView: Asked to create child view model for view that's not a child view. This is a bug!");
            }

            // Create components model
            Dictionary<int, Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>> componentMap = CreateComponentMap();
            RealtimeViewComponentsModel componentsModel = CreateComponentsModel(componentMap);

            // Create child views model
            Dictionary<int, RealtimeView> childViewsMap = CreateChildViewMap();
            RealtimeViewComponentsModel childViewsModel = CreateChildViewsModel(childViewsMap);
    
            // Create RealtimeViewModel
            RealtimeViewModel viewModel = new RealtimeViewModel(componentsModel, childViewsModel);
    
            return viewModel;
        }
    
        public RealtimeViewComponentsModel _CreateComponentsModel() {
            // Get component map
            Dictionary<int, Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>> componentMap = CreateComponentMap();
    
            return CreateComponentsModel(componentMap);
        }

        public RealtimeViewComponentsModel _CreateChildViewsModel() {
            // Get child views map
            Dictionary<int, RealtimeView> childViewMap = CreateChildViewMap();
    
            return CreateChildViewsModel(childViewMap);
        }
    
        private RealtimeViewComponentsModel CreateComponentsModel(Dictionary<int, Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>> componentMap) {
            // Create models for all components
            Dictionary<int, IModel> componentModelMap = new Dictionary<int, IModel>();
            foreach (KeyValuePair<int, Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>> pair in componentMap) {
                int           componentID       = pair.Key;
                Component     component         = pair.Value.First;
                MethodInfo    createModelMethod = pair.Value.Second;
                PropertyInfo  modelProperty     = pair.Value.Third;
    
                // Create model
                object componentModelObject = null;
                if (createModelMethod != null) {
                    componentModelObject = createModelMethod.Invoke(null, null);
                    if (componentModelObject == null) {
                        Debug.LogError("Model supplied by MonoBehaviour's CreateModel method is null. Skipping component: (" + componentID + ":" + component + ")", component);
                        continue;
                    }
                } else {
                    // Create it using the type from the model property.
                    Type modelType = modelProperty.PropertyType;
                    
                    try {
                        componentModelObject = Activator.CreateInstance(modelType);
                    } catch (MissingMethodException) {
                        Debug.LogError("MonoBehaviour doesn't have CreateModel method, and model type (" + modelType + ") doesn't have a public default constructor. Skipping component: (" + componentID + ":" + component + ")", component);
                        continue;
                    } catch (Exception exception) {
                        Debug.LogError("MonoBehaviour doesn't have CreateModel method, and Realtime was unable to create a model instance. Skipping component: (" + componentID + ":" + component + ") (" + exception + ")", component);
                        continue;
                    }
                }
    
                // Verify model implements IModel
                IModel componentModel = componentModelObject as IModel;
                if (componentModel == null) {
                    Debug.LogError("Model created by MonoBehaviour (" + componentModelObject.GetType() + ") doesn't implement IModel interface. Skipping component: (" + componentID + ":" + component + ")", component);
                    continue;
                }
    
                // Set model
                componentModelMap[componentID] = componentModel;
            }
    
            // Create RealtimeViewComponentsModel
            RealtimeViewComponentsModel componentsModel = new RealtimeViewComponentsModel(componentModelMap);
    
            return componentsModel;
        }
    
        private Dictionary<int, Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>> CreateComponentMap() {
            Dictionary<int, Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>> componentMap = new Dictionary<int, Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>>();
    
            // Loop through all components, ignore invalid ones.
            foreach (RealtimeViewIDComponentPair pair in _components) {
                int           componentID = pair.componentID;
                MonoBehaviour component   = pair.component;
    
                // Check for valid component ID (greater than zero)
                // TODO: We need to verify the upper bound limit on componentIDs too
                if (componentID <= 0) {
                    Debug.LogError("RealtimeView components must have a componentID of 1 or greater. Skipping component: (" + componentID + ":" + component + ")", component);
                    continue;
                }
    
                // Make sure component is valid
                if (component == null) {
                    // Note: Deprecated component IDs will have a null view property. This is normal and they can be safely ignored.
                    continue;
                }
    
                // Get CreateModel method if it exists
                MethodInfo createModelMethod = component.GetType().GetMethod("CreateModel", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    
                // Get model property
                PropertyInfo modelProperty = component.GetType().GetProperty("model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (modelProperty == null) {
                    Debug.LogError("MonoBehaviour doesn't have a \"model\" property. Skipping component: (" + componentID + ":" + component + ")", component);
                    continue;
                }
    
                // Get realtimeView property
                PropertyInfo realtimeViewProperty = component.GetType().GetProperty("realtimeView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (realtimeViewProperty != null)
                    realtimeViewProperty = realtimeViewProperty.DeclaringType.GetProperty("realtimeView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                // Check for duplicate
                if (componentMap.Remove(componentID)) {
                    Debug.LogError("Found duplicate componentID (" + componentID + "). Skipping both components to avoid data corruption.", component);
                    continue;
                }
    
                componentMap.Add(componentID, new Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>(component, createModelMethod, modelProperty, realtimeViewProperty));
            }
    
            return componentMap;
        }

        private RealtimeViewComponentsModel CreateChildViewsModel(Dictionary<int, RealtimeView> childViewMap) {
            // Create models for all views
            Dictionary<int, IModel> childViewModelMap = new Dictionary<int, IModel>();
            foreach (KeyValuePair<int, RealtimeView> pair in childViewMap) {
                int          viewID       = pair.Key;
                RealtimeView view         = pair.Value;

                if (view == null) {
                    // Note: Deprecated view IDs will have a null view property. This is normal and they can be safely ignored.
                    continue;
                }
    
                // Create model
                RealtimeViewModel viewModel = view._CreateChildViewModel();
                if (viewModel == null) {
                    Debug.LogError("Model supplied by child RealtimeView is null. Skipping view: (" + viewID + ":" + view + ")", view);
                    continue;
                }
    
                // Set model
                childViewModelMap[viewID] = viewModel;
            }
    
            // Create RealtimeViewComponentsModel
            RealtimeViewComponentsModel childViewsModel = new RealtimeViewComponentsModel(childViewModelMap);
    
            return childViewsModel;
        }

        private Dictionary<int, RealtimeView> CreateChildViewMap() {
            Dictionary<int, RealtimeView> childViewMap = new Dictionary<int, RealtimeView>();
    
            // Loop through all child views, ignore invalid ones.
            foreach (RealtimeViewChildIDViewPair pair in _childViews) {
                int          viewID = pair.viewID;
                RealtimeView view   = pair.view;
    
                // Check for valid view ID (greater than zero)
                // TODO: We need to verify the upper bound limit on viewIDs too
                if (viewID <= 0) {
                    Debug.LogError("RealtimeView child views must have an ID of 1 or greater. Skipping view: (" + viewID + ":" + view + ")", view);
                    continue;
                }
    
                // Check for duplicate
                if (childViewMap.Remove(viewID)) {
                    Debug.LogError("Found duplicate child view ID (" + viewID + "). Skipping both views to avoid data corruption.", view);
                    continue;
                }
    
                childViewMap.Add(viewID, view);
            }
    
            return childViewMap;
        }
    
        private void SetModel(RealtimeViewModel model) {
            if (model == _model)
                return;
    
            if (_model != null) {
                _model._SetRealtimeView(null);
            }
            
            _model = model;
    
            if (_model != null) {
                _model._SetRealtimeView(this);
    
                Dictionary<int, Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>> componentMap = CreateComponentMap();

                // Create components model if needed (can happen if RealtimeViewModel existed in the datastore but hasn't been linked to a RealtimeView yet)
                RealtimeViewComponentsModel componentsModel = _model.componentsModel;
                if (_model.componentsModel == null) {
                    componentsModel = CreateComponentsModel(componentMap);
                    _model.SetComponentsModelAndDeserializeCachedModelsIfNeeded(componentsModel);
                }
    
                // Loop through components and assign models
                foreach (KeyValuePair<int, Tuple<Component, MethodInfo, PropertyInfo, PropertyInfo>> pair in componentMap) {
                    int           componentID          = pair.Key;
                    Component     component            = pair.Value.First;
                    MethodInfo    createModelMethod    = pair.Value.Second;
                    PropertyInfo  modelProperty        = pair.Value.Third;
                    PropertyInfo  realtimeViewProperty = pair.Value.Fourth;
    
                    IModel componentModel = componentsModel[componentID];
                    if (componentModel == null) {
                        Debug.LogError("RealtimeView is attempting to connect a component to its model, but cannot find model for component: (" + componentID + ":" + component + "). This is a bug!", component);
                        continue;
                    }
    
                    // Set realtime view reference on object if it supports it
                    if (realtimeViewProperty != null)
                        realtimeViewProperty.SetValue(component, this, null);
    
                    // Set model on component
                    modelProperty.SetValue(component, componentModel, null);
                }

                Dictionary<int, RealtimeView> childViewMap = CreateChildViewMap();
                    
                // Create child views model if needed (can happen if RealtimeViewModel existed in the datastore but hasn't been linked to a RealtimeView yet)
                RealtimeViewComponentsModel childViewsModel = _model.childViewsModel;
                if (childViewsModel == null) {
                    childViewsModel = CreateChildViewsModel(childViewMap);
                    _model.SetChildViewsModelAndDeserializeCachedModelsIfNeeded(childViewsModel);
                }
    
                // Loop through child views and assign models
                foreach (KeyValuePair<int, RealtimeView> pair in childViewMap) {
                    int           viewID          = pair.Key;
                    RealtimeView  view            = pair.Value;
        
                    RealtimeViewModel viewModel = childViewsModel[viewID] as RealtimeViewModel;
                    if (viewModel == null) {
                        Debug.LogError("RealtimeView attempting to connect child view to its models, but cannot find model for view: (" + viewID + ":" + view + "). This is a bug!", view);
                        continue;
                    }
    
                    // Set realtime instance and model
                    view._SetRealtime(_realtime);
                    view.model = viewModel;
                }
            }
        }
    
        public void RequestOwnership() {
            _model.RequestOwnership(realtime.clientID);
        }
    
        public void ClearOwnership() {
            _model.ClearOwnership();
        }

        private class Tuple<T1, T2, T3> {
            public T1 First  { get; private set; }
            public T2 Second { get; private set; }
            public T3 Third  { get; private set; }
            public Tuple(T1 first, T2 second, T3 third) {
                First  = first;
                Second = second;
                Third  = third;
            }
        }
        
        private class Tuple<T1, T2, T3, T4> {
            public T1 First  { get; private set; }
            public T2 Second { get; private set; }
            public T3 Third  { get; private set; }
            public T4 Fourth { get; private set; }
            public Tuple(T1 first, T2 second, T3 third, T4 fourth) {
                First  = first;
                Second = second;
                Third  = third;
                Fourth = fourth;
            }
        }
    }
}
