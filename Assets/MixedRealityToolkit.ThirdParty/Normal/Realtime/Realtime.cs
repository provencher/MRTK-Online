using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

using Normal.Realtime.Serialization;

namespace Normal.Realtime {
    public class Realtime : MonoBehaviour {
        // Class
        public  static HashSet<Realtime>   instances { get { return __instances; } }
        private static HashSet<Realtime> __instances;
        static Realtime() {
#if UNITY_EDITOR
            EditorApplication.update += EditorUpdate;
#    if UNITY_2017_3_OR_NEWER
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
#    endif
#endif
            // Keep track of all instances of Realtime
            __instances = new HashSet<Realtime>();
        }

#if UNITY_EDITOR
        private static void EditorUpdate() {
            if (EditorApplication.isCompiling && EditorApplication.isPlaying) {
                EditorApplication.isPlaying = false;
            }
        }

#if UNITY_2017_3_OR_NEWER
        private static void PlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredPlayMode) {
                EditorApplication.LockReloadAssemblies();
            } else if (state == PlayModeStateChange.EnteredEditMode) {
                EditorApplication.UnlockReloadAssemblies();
            }
        }
#endif
#endif
        public static GameObject Instantiate(string prefabName, Realtime useInstance) {
            return Instantiate(prefabName, useInstance: useInstance);
        }

        public static GameObject Instantiate(string prefabName, bool ownedByClient = true, bool preventOwnershipTakeover = false, bool destroyWhenOwnerOrLastClientLeaves = true, Realtime useInstance = null) {
            if (useInstance == null) {
                if (__instances.Count == 0) {
                    Debug.LogError("Realtime: Unable to instantiate prefab. No instances of Realtime exist in the scene. Please specify a specific instance of Realtime when calling Instantiate()");
                    return null;
                }
                if (__instances.Count > 1) {
                    Debug.LogError("Realtime: Multiple instances of Realtime exist in the scene. ");
                    return null;
                }
                foreach (Realtime instance in __instances) {
                    useInstance = instance;
                    break;
                }
            }

            return useInstance._Instantiate(prefabName, ownedByClient, preventOwnershipTakeover, destroyWhenOwnerOrLastClientLeaves);
        }

        public static GameObject Instantiate(string prefabName, Vector3 position, Quaternion rotation, bool ownedByClient = true, bool preventOwnershipTakeover = false, bool destroyWhenOwnerOrLastClientLeaves = true, Realtime useInstance = null) {
            GameObject gameObject = Instantiate(prefabName, ownedByClient, preventOwnershipTakeover, destroyWhenOwnerOrLastClientLeaves, useInstance);

            if (gameObject != null) {
                RealtimeTransform realtimeTransform = gameObject.GetComponent<RealtimeTransform>();
                if (realtimeTransform != null) {
                    realtimeTransform.RequestOwnership();
                    gameObject.transform.position = position;
                    gameObject.transform.rotation = rotation;
                } else {
                    Debug.LogWarning("Realtime: Instantiate() asked to set position & rotation on prefab that doesn't have a RealtimeTransform component. The position / rotation will not be synchronized between clients.");
                }
            }

            return gameObject;
        }

        public static void Destroy(GameObject gameObject) {
            if (gameObject == null) {
                Debug.LogError("Realtime asked to destroy game object, but the game object is null.");
                return;
            }

            RealtimeView realtimeView = gameObject.GetComponent<RealtimeView>();
            if (realtimeView == null) {
                Debug.LogError("Realtime asked to destroy game object, but the game object does not contain a RealtimeView component.");
                return;
            }

            Destroy(realtimeView);
        }

        public static void Destroy(RealtimeView realtimeView) {
            Realtime realtime = realtimeView.realtime;
            if (realtime == null) {
                Debug.LogError("Realtime asked to destroy RealtimeView, but the realtime view isn't associated with an instance of Realtime. Was it instantiated using Realtime.Instantiate() ?");
                return;
            }

            realtime.DestroyRealtimeView(realtimeView);
        }

        // Hide the built-in Instantiate methods
        [Obsolete("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName).")]
        [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeOfFirstArgument)]
        public static new UnityEngine.Object Instantiate(UnityEngine.Object original, Vector3 position, Quaternion rotation, Transform parent) { throw new NotImplementedException("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName)."); }
        [Obsolete("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName).")]
        [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeOfFirstArgument)]
        public static new UnityEngine.Object Instantiate(UnityEngine.Object original)                                                          { throw new NotImplementedException("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName)."); }
        [Obsolete("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName).")]
        [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeOfFirstArgument)]
        public static new UnityEngine.Object Instantiate(UnityEngine.Object original, Vector3 position, Quaternion rotation)                   { throw new NotImplementedException("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName)."); }
        [Obsolete("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName).")]
        public static new T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : UnityEngine.Object                 { throw new NotImplementedException("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName)."); }
        [Obsolete("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName).")]
        public static new T Instantiate<T>(T original, Transform parent) where T : UnityEngine.Object                                          { throw new NotImplementedException("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName)."); }
        [Obsolete("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName).")]
        public static new T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : UnityEngine.Object   { throw new NotImplementedException("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName)."); }
        [Obsolete("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName).")]
        public static new T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : UnityEngine.Object                     { throw new NotImplementedException("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName)."); }
        [Obsolete("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName).")]
        public static GameObject Instantiate(GameObject original)                                                              { throw new NotImplementedException("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName)."); }
        [Obsolete("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName).")]
        [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeOfFirstArgument)]
        public static new UnityEngine.Object Instantiate(UnityEngine.Object original, Transform parent, bool instantiateInWorldSpace)          { throw new NotImplementedException("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName)."); }
        [Obsolete("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName).")]
        [UnityEngineInternal.TypeInferenceRule(UnityEngineInternal.TypeInferenceRules.TypeOfFirstArgument)]
        public static new UnityEngine.Object Instantiate(UnityEngine.Object original, Transform parent)                                        { throw new NotImplementedException("This version of Realtime.Instantiate() is not supported. Please use Realtime.Instantiate(string prefabName)."); }

        // Instance
        public delegate void RealtimeEvent(Realtime realtime);
        public event RealtimeEvent didConnectToRoom;
        public event RealtimeEvent didDisconnectFromRoom;

        [SerializeField] private string _appKey = "";
        [SerializeField] private string _roomToJoinOnStart = "Test Room";
        [SerializeField] private bool   _joinRoomOnStart   = true;
        [SerializeField] private bool   _debugLogging      = false;

        private Room _room;
        public  Room  room { get { return _room; } set { SetRoom(value); } }

        public bool connecting   { get { if (_room == null) return false; return _room.connecting;   } }
        public bool connected    { get { if (_room == null) return false; return _room.connected;    } }
        public bool disconnected { get { if (_room == null) return false; return _room.disconnected; } }

        public int clientID { get { return _room != null ? _room.clientID : -1; } }

        // Scene Views
        private HashSet<RealtimeView> _sceneViews;

        // Prefab Views
        private HashSet<RealtimeView> _prefabViews;
        private GameObject _lastPrefabInstantiated;

        //// Instance
        // Unity Events
        private void Awake() {
            // Create hash set to hold all scene realtime views
            if (_sceneViews == null)
                _sceneViews = new HashSet<RealtimeView>();

            // Create hash set to hold all prefab realtime views
            _prefabViews = new HashSet<RealtimeView>();

            // Register this instance
            __instances.Add(this);
        }

        private void Start() {
            if (_joinRoomOnStart)
                Connect(_roomToJoinOnStart, null);
        }

        private void OnDestroy() {
            // Unregister this instance
            __instances.Remove(this);

            // Disconnect
            Disconnect();

            // Destroy room
            SetRoom(null);
        }

        private void OnApplicationQuit() {
            // Unregister this instance
            __instances.Remove(this);

            // Disconnect
            Disconnect();

            // Destroy room
            SetRoom(null);
        }

        private void Update() {
            // TODO: Make an editor script that complains if Run In Background isn't set. If we don't run in the background, we don't tick and we get disconnected.
            // TODO: Experiment with only calling tick once every 1/30th of a second. Make sure audio still works nicely!
            if (_room != null) {
                _room.debugLogging = _debugLogging;
                _room.Tick(Time.deltaTime);
            }
        }

        // Events
        private void FireDidConnectToRoom() {
            try {
                if (didConnectToRoom != null)
                    didConnectToRoom(this);
            } catch (Exception exception) {
                Debug.LogException(exception);
            }
        }

        private void FireDidDisconnectFromRoom() {
            try {
                if (didDisconnectFromRoom != null)
                    didDisconnectFromRoom(this);
            } catch (Exception exception) {
                Debug.LogException(exception);
            }
        }

        // Room
        public void Connect(string roomName, IModel roomModel = null) {
            if (_room == null)
                SetRoom(new Room());

            // Connect to the room
            _room.Connect(roomName, _appKey, roomModel);
        }

        public void Disconnect() {
            if (_room == null)
                return;

            _room.Disconnect();
        }

        void RoomConnectionStateChanged(Room room, Room.ConnectionState previousConnectionState, Room.ConnectionState connectionState) {
            switch (connectionState) {
                case Room.ConnectionState.Ready:
                    // Connect scene views
                    ConnectSceneViewsToDatastore();
                    
                    // Fire connect event
                    FireDidConnectToRoom();
                    break;
                case Room.ConnectionState.Disconnected:
                case Room.ConnectionState.Error:
                    // Fire disconnect event
                    FireDidDisconnectFromRoom();

                    // Disconnect scene views
                    DisconnectSceneViewsFromDatastore();

                    // Destroy prefab views
                    DestroyAllPrefabRealtimeViews();
                    break;
            }
        }

        void SetRoom(Room room) {
            if (_room != null) {
                if (_room.connectionState == Room.ConnectionState.Ready) {
                    // Fire disconnect event
                    FireDidDisconnectFromRoom();
                }

                // Unregister for connection and datastore events
                _room.connectionStateChanged -= RoomConnectionStateChanged;
                _room.datastore.prefabRealtimeViewModelAdded   -= PrefabRealtimeViewModelAdded;
                _room.datastore.prefabRealtimeViewModelRemoved -= PrefabRealtimeViewModelRemoved;

                // Destroy prefab views
                DestroyAllPrefabRealtimeViews();

                // Disconnect scene views
                DisconnectSceneViewsFromDatastore();

                // Clear realtime reference
                _room._SetRealtime(null);
            }

            _room = room;

            if (_room != null) {
                // Remove room from existing Realtime instance if it's bound to one.
                if (_room.realtime != null && _room.realtime != this)
                    _room.realtime.SetRoom(null);

                // Set reference to realtime (only used to prevent multiple Realtime instances from using the same Room object)
                _room._SetRealtime(this);
                _room.debugLogging = _debugLogging;

                // Register for connection and datastore events
                _room.connectionStateChanged += RoomConnectionStateChanged;
                _room.datastore.prefabRealtimeViewModelAdded   += PrefabRealtimeViewModelAdded;
                _room.datastore.prefabRealtimeViewModelRemoved += PrefabRealtimeViewModelRemoved;

                // Connect scene views
                ConnectSceneViewsToDatastore();

                // Create prefab views
                CreatePrefabRealtimeViewsForDatastore();

                if (_room.connectionState == Room.ConnectionState.Ready) {
                    // Fire connect event
                    FireDidConnectToRoom();
                }
            }
        }

        // Scene Realtime Views
        public void _RegisterSceneRealtimeView(RealtimeView view) {
            if (view.sceneViewUUID == null || view.sceneViewUUID.Length == 0) {
                Debug.LogError("Realtime: Attempting to register RealtimeView as a scene view, but it doesn't have a proper UUID. Ignoring. This is a bug!");
                return;
            }

            // Create hash set to hold all scene realtime views in if needed
            if (_sceneViews == null)
                _sceneViews = new HashSet<RealtimeView>();

            // Check for duplicate UUIDs
            foreach (RealtimeView sceneView in _sceneViews) {
                if (sceneView.sceneViewUUID.SequenceEqual(view.sceneViewUUID)) {
                    Debug.LogError("Realtime: RealtimeView attempting to register with a UUID that has already been registered! This means there are multiple RealtimeViews in the scene with the same UUID. Did you additively load a copy of the same scene? Make sure to reset the UUID for each RealtimeView under Advanced Settings. This RealtimeView will be ignored.");
                    return;
                }
            }
            
            // Add to scene view collection
            _sceneViews.Add(view);

            // If we're already connected, then link up this scene view to its model in the room datastore
            if (_room != null && _room.connectionState == Room.ConnectionState.Ready) {
                ConnectSceneViewToDatastore(view);
            } else {
                // If we're not connected, we should at least give this view a fresh model to read off of in the meantime
                ReplaceSceneViewModelWithFreshModel(view);
            }
        }

        public void _UnregisterSceneRealtimeView(RealtimeView view) {
            if (!_sceneViews.Remove(view)) {
                Debug.LogError("Realtime: RealtimeView attempting to unregister, but is not found in this instance of Realtime's scene view list.");
                return;
            }

            // Repplace model on view so it doesn't mess with the datastore one
            ReplaceSceneViewModelWithFreshModel(view);
        }

        private void ConnectSceneViewsToDatastore() {
            // The room is already connected, connect every scene RealtimeView to the datastore.
            if (_room.connectionState == Room.ConnectionState.Ready) {
                foreach (RealtimeView view in _sceneViews) {
                    ConnectSceneViewToDatastore(view);
                }
            }
        }

        private void ConnectSceneViewToDatastore(RealtimeView view) {
            if (_room.connectionState != Room.ConnectionState.Ready) {
                Debug.LogError("Failed to connect scene RealtimeView to model. Not connected to room... This is a bug!");
                return;
            }

            if (view.sceneViewUUID == null || view.sceneViewUUID.Length == 0) {
                Debug.LogError("Realtime: Attempting to connect scene RealtimeView to the datastore, but it doesn't have a proper UUID. Ignoring. This is a bug!");
                return;
            }

            RealtimeViewModel viewModel = _room.datastore.GetSceneRealtimeViewModelForUUID(view.sceneViewUUID);
            
            // Create a view model for this scene object in the datastore
            if (viewModel == null) {
                viewModel = view._CreateRootSceneViewModel();

                // Add to datastore        
                if (!_room.datastore.AddSceneRealtimeViewModel(viewModel)) {
                    Debug.LogError("Unable to add scene RealtimeView's model to the room datastore to synchronize. This is a bug!");
                    return;
                }
            }

            // At this point we have a valid viewModel that exists in the datastore. Set it on the view.
            view.model = viewModel;
        }

        private void DisconnectSceneViewsFromDatastore() {
            foreach (RealtimeView view in _sceneViews) {
                ReplaceSceneViewModelWithFreshModel(view);
            }
        }

        private void ReplaceSceneViewModelWithFreshModel(RealtimeView view) {
            // Replace view model with clean / fresh view model.
            view.model = view._CreateRootSceneViewModel();
        }

        // Prefab Realtime Views
        private GameObject _Instantiate(string prefabName, bool ownedByClient = true, bool preventOwnershipTakeover = false, bool destroyWhenOwnerOrLastClientLeaves = true) {
            if (_room == null) {
                Debug.LogError("Realtime asked to instantiate game object, but is not associated with a room! Ignoring.");
                return null;
            }

            if (_room.connectionState != Room.ConnectionState.Ready) {
                Debug.LogError("Realtime asked to instantiate game object, but we're not connected to a room! Ignoring. (Room: " + _room.connectionState + ")");
                return null;
            }

            // Load the prefab
            GameObject prefab = Resources.Load<GameObject>(prefabName);
            if (prefab == null) {
                Debug.LogError("Failed to find prefab \"" + prefabName + "\". Make sure it's in a Resources folder. Bailing.");
                return null;
            }

            // Get the RealtimeView script at the root
            RealtimeView prefabRealtimeView = prefab.GetComponent<RealtimeView>();
            if (prefabRealtimeView == null) {
                Debug.LogError("Failed to find RealtimeView script on prefab \"" + prefabName + "\". Make sure the prefab has a RealtimeView script at the root level. Bailing.");
                return null;
            }

            // Clear this just to be safe
            _lastPrefabInstantiated = null;

            // Ownership / lifetime flags
            int  ownerID = ownedByClient ? clientID : -1;
            uint lifetimeFlags = 0;

            if (preventOwnershipTakeover)
                lifetimeFlags |= (uint)MetaModel.LifetimeFlags.PreventOwnershipTakeover;
            
            if (destroyWhenOwnerOrLastClientLeaves)
                lifetimeFlags |= (uint)MetaModel.LifetimeFlags.DestroyWhenOwnerOrLastClientLeaves;

            // Add model to datastore for this prefab
            _room.datastore.AddPrefabRealtimeViewModel(prefabRealtimeView._CreateRootPrefabViewModel(prefabName, ownerID, lifetimeFlags));

            // At this point, PrefabRealtimeViewModelAdded has fired for the model above, created the prefab, and stored a reference to it in _lastPrefabInstantiated

            return _lastPrefabInstantiated;
        }

        private void DestroyRealtimeView(RealtimeView realtimeView) {
            if (_room == null) {
                // Note: I commented this out, because if scene objects attempt to destroy realtime views inside of
                //       OnDestroy, they'll fire after Realtime has already disconnected and destroyed the room.
                //       Afaik, there's no way for them to detect that, so we fail silently here.
                //Debug.LogError("Realtime: Unable to destroy RealtimeView because this Realtime instance is not associated with a room! Ignoring.");
                return;
            }

            if (_room.connectionState != Room.ConnectionState.Ready) {
                Debug.LogError("Realtime: Unable to destroy RealtimeView because we're not connected to a room! Ignoring. (Room: " + _room.connectionState + ")");
                return;
            }

            RealtimeViewModel model = realtimeView.model;
            if (model == null) {
                Debug.LogError("Realtime: Unable to destroy RealtimeView because it doesn't have a model property associated with it.");
                return;
            }

            if (!_room.datastore.RemovePrefabRealtimeViewModel(model)) {
                Debug.LogError("Realtime: Could not find RealtimeViewModel for RealtimeView in the datastore. Unable to destroy RealtimeView.");
                return;
            }
        }

        void PrefabRealtimeViewModelAdded(Datastore datastore, RealtimeViewModel model, bool remote) {
            // Create a realtime view for this realtime view model.
            GameObject gameObject = CreatePrefabForRealtimeViewModel(model);

            // If this is a local call, store the game object so Instantiate() can return a reference to it
            if (!remote)
                _lastPrefabInstantiated = gameObject;
        }

        void PrefabRealtimeViewModelRemoved(Datastore datastore, RealtimeViewModel model, bool remote) {
            if (model.realtimeView != null) {
                _prefabViews.Remove(model.realtimeView);
                UnityEngine.Object.Destroy(model.realtimeView.gameObject);
            } else {
                Debug.LogError("Realtime: RealtimeViewModel was deleted from datastore, but has no corresponding prefab. This is a bug.");
            }
        }

        private void CreatePrefabRealtimeViewsForDatastore() {
            DestroyAllPrefabRealtimeViews();

            // The room is already connected, create a RealtimeView for every model in the datastore.
            if (_room.connectionState == Room.ConnectionState.Ready) {
                foreach (RealtimeViewModel prefabViewModel in _room.datastore.prefabViewModels) {
                    CreatePrefabForRealtimeViewModel(prefabViewModel);
                }
            }
        }

        private void DestroyAllPrefabRealtimeViews() {
            foreach (RealtimeView prefabView in _prefabViews) {
                UnityEngine.Object.Destroy(prefabView.gameObject);
            }
            _prefabViews.Clear();
        }

        private GameObject CreatePrefabForRealtimeViewModel(RealtimeViewModel model) {
            string prefabName = model.prefabName;

            // Load the prefab
            GameObject prefab = Resources.Load<GameObject>(prefabName);
            if (prefab == null) {
                Debug.LogError("Failed to find prefab \"" + prefabName + "\". Make sure it's in a Resources folder. Bailing.");
                return null;
            }

            // Get the RealtimeView script at the root
            RealtimeView prefabRealtimeView = prefab.GetComponent<RealtimeView>();
            if (prefabRealtimeView == null) {
                Debug.LogError("Attempting to instantiate prefab from datastore. Failed to find RealtimeView script on prefab \"" + prefabName + "\". Make sure the prefab has a RealtimeView script at the root level. Bailing.");
                return null;
            }

            // Instantiate and assign the model
            GameObject     gameObject   = GameObject.Instantiate(prefab);
            RealtimeView   realtimeView = gameObject.GetComponent<RealtimeView>();
            realtimeView._SetRealtime(this);
            realtimeView.model = model;

            // Add to hash set of prefab realtime views.
            _prefabViews.Add(realtimeView);

            return gameObject;
        }
    }
}
