using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime.Serialization;

namespace Normal.Realtime {
    public class RealtimeViewModel : IModel {
        private RealtimeView _realtimeView;
        public  RealtimeView  realtimeView { get { return _realtimeView; } }
    
        public int  ownerID       { get { return _metaModel.ownerID;       } set { _metaModel.ownerID = value;       } }
        public uint lifetimeFlags { get { return _metaModel.lifetimeFlags; } set { _metaModel.lifetimeFlags = value; } }
    
        private MetaModel _metaModel;
    
        private byte[] _sceneViewUUID;
        public  byte[]  sceneViewUUID { get { return _sceneViewUUID; } }
    
        private string _prefabName;
        public  string  prefabName { get { return _prefabName; } }
    
        private struct CachedDeltaUpdate {
            public readonly uint updateID;
            public readonly ReadBuffer buffer;
            public CachedDeltaUpdate(uint updateID, ReadBuffer buffer) {
                this.updateID = updateID;
                this.buffer   = buffer;
            }
        }
        // Components
        private RealtimeViewComponentsModel       _componentsModel;
        public  RealtimeViewComponentsModel        componentsModel { get { return _componentsModel; } }
        // Used to store the components model data until we actually have a model to deserialize into
        private ReadBuffer                  _cachedComponentsModel;
        private List<CachedDeltaUpdate>     _cachedComponentsModelDeltaUpdates;

        // Child Views
        private RealtimeViewComponentsModel       _childViewsModel;
        public  RealtimeViewComponentsModel        childViewsModel { get { return _childViewsModel; } }
        // Used to store the components model data until we actually have a model to deserialize into
        private ReadBuffer                  _cachedChildViewsModel;
        private List<CachedDeltaUpdate>     _cachedChildViewsModelDeltaUpdates;
    
        public RealtimeViewModel() {
            _metaModel = new MetaModel();
        }
    
        // Scene Realtime View
        public RealtimeViewModel(byte[] sceneViewUUID, int ownerID, uint lifetimeFlags, RealtimeViewComponentsModel componentsModel, RealtimeViewComponentsModel childViewsModel) {
            _sceneViewUUID = sceneViewUUID;
    
            _metaModel = new MetaModel();
            _metaModel.ownerID       = ownerID;
            _metaModel.lifetimeFlags = lifetimeFlags;
    
            _componentsModel = componentsModel;
            _childViewsModel = childViewsModel;
        }
    
        // Prefab Realtime View
        public RealtimeViewModel(string prefabName, int ownerID, uint lifetimeFlags, RealtimeViewComponentsModel componentsModel, RealtimeViewComponentsModel childViewsModel) {
            _prefabName = prefabName;
    
            _metaModel = new MetaModel();
            _metaModel.ownerID       = ownerID;
            _metaModel.lifetimeFlags = lifetimeFlags;
    
            _componentsModel = componentsModel;
            _childViewsModel = childViewsModel;
        }

        // Prefab Child Realtime View
        public RealtimeViewModel(RealtimeViewComponentsModel componentsModel, RealtimeViewComponentsModel childViewsModel) {
            _metaModel = new MetaModel();
    
            _componentsModel = componentsModel;
            _childViewsModel = childViewsModel;
        }
    
        public void _SetRealtimeView(RealtimeView realtimeView) {
            _realtimeView = realtimeView;
        }
    
        // Ownership
        public void RequestOwnership(int clientIndex) {
            _metaModel.ownerID = clientIndex;
        }
    
        public void ClearOwnership() {
            _metaModel.ownerID = -1;
        }
    
    
        // Components
        public void SetComponentsModelAndDeserializeCachedModelsIfNeeded(RealtimeViewComponentsModel componentsModel) {
            if (_componentsModel != null) {
                Debug.LogError("Attempting to deserialize cached components model on RealtimeViewModel that already has a components model. This is a bug!");
                return;
            }
            _componentsModel = componentsModel;
    
            DeserializeCachedComponentsModelIfNeeded();
        }

        public void SetChildViewsModelAndDeserializeCachedModelsIfNeeded(RealtimeViewComponentsModel childViewsModel) {
            if (_childViewsModel != null) {
                Debug.LogError("Attempting to deserialize cached child views model on RealtimeViewModel that already has a child views model. This is a bug!");
                return;
            }
            _childViewsModel = childViewsModel;
    
            DeserializeCachedChildViewsModelIfNeeded();
        }
    
        private void CreateComponentsModelAndChildViewsModelIfNeeded(string prefabName) {
            // Load the prefab
            GameObject prefab = Resources.Load<GameObject>(prefabName);
            if (prefab == null) {
                Debug.LogError("Attempting to instantiate prefab from datastore. Failed to find prefab \"" + prefabName + "\". Make sure it's in a Resources folder. Bailing.");
                return;
            }

            // Get the RealtimeView script at the root
            RealtimeView prefabRealtimeView = prefab.GetComponent<RealtimeView>();
            if (prefabRealtimeView == null) {
                Debug.LogError("Attempting to instantiate prefab from datastore. Failed to find RealtimeView component on prefab \"" + prefabName + "\". Make sure the prefab has a RealtimeView script at the root level. Bailing.");
                return;
            }
    
            if (_componentsModel == null)
                _componentsModel = prefabRealtimeView._CreateComponentsModel();
            if (_childViewsModel == null)
                _childViewsModel = prefabRealtimeView._CreateChildViewsModel();
        }
    
        private void DeserializeCachedComponentsModelIfNeeded() {
            if (_cachedComponentsModel == null)
                return;
    
            if (_componentsModel == null) {
                Debug.LogError("RealtimeViewModel asked to read cached components model, but doesn't have a components model to read into. This is a bug!");
                return;
            }
    
            // Deserialize components model
            ReadStream cachedComponentsModelStream = new ReadStream(_cachedComponentsModel);
            cachedComponentsModelStream.DeserializeRootModel(_componentsModel);
            _cachedComponentsModel = null;
    
            // Deserialize components model delta updates
            foreach (CachedDeltaUpdate cachedComponentsModelDeltaUpdate in _cachedComponentsModelDeltaUpdates) {
                cachedComponentsModelStream = new ReadStream(cachedComponentsModelDeltaUpdate.buffer);
                cachedComponentsModelStream.DeserializeRootModelDeltaUpdates(_componentsModel, true, cachedComponentsModelDeltaUpdate.updateID);
            }
            _cachedComponentsModelDeltaUpdates = null;
        }

        private void DeserializeCachedChildViewsModelIfNeeded() {
            if (_cachedChildViewsModel == null)
                return;
            
            if (_childViewsModel == null) {
                Debug.LogError("RealtimeViewModel asked to read cached child views model, but doesn't have a child views model to read into. This is a bug!");
                return;
            }
    
            // Deserialize child views model
            ReadStream cachedChildViewsModelStream = new ReadStream(_cachedChildViewsModel);
            cachedChildViewsModelStream.DeserializeRootModel(_childViewsModel);
            _cachedChildViewsModel = null;
    
            // Deserialize child views model delta updates
            foreach (CachedDeltaUpdate cachedChildViewsModelDeltaUpdate in _cachedChildViewsModelDeltaUpdates) {
                cachedChildViewsModelStream = new ReadStream(cachedChildViewsModelDeltaUpdate.buffer);
                cachedChildViewsModelStream.DeserializeRootModelDeltaUpdates(_childViewsModel, true, cachedChildViewsModelDeltaUpdate.updateID);
            }
            _cachedChildViewsModelDeltaUpdates = null;
        }
        
        // Serialization
        enum PropertyID {
            SceneViewUUID = 1,
            PrefabName    = 2,
            Components    = 3,
            ChildViews    = 4,
        }
        
        public int WriteLength(StreamContext context) {
            int length = 0;
    
            // Meta model
            length += WriteStream.WriteModelLength(0, _metaModel, context);
    
            if (context.fullModel) {
                // Write all properties
                if (_sceneViewUUID != null && _sceneViewUUID.Length > 0)
                    length += WriteStream.WriteBytesLength((uint)PropertyID.SceneViewUUID, _sceneViewUUID.Length);
                if (_prefabName != null && _prefabName.Length > 0)
                    length += WriteStream.WriteStringLength((uint)PropertyID.PrefabName, _prefabName);
            }
    
            // Components
            if (_componentsModel != null)
                length += WriteStream.WriteModelLength((uint)PropertyID.Components, _componentsModel, context);
            
            // Child Views
            if (_childViewsModel != null)
                length += WriteStream.WriteModelLength((uint)PropertyID.ChildViews, _childViewsModel, context);
            
            return length;
        }
        
        public void Write(WriteStream stream, StreamContext context) {
            // Meta model
            stream.WriteModel(0, _metaModel, context);
    
            if (context.fullModel) {
                // Write all properties
                if (_sceneViewUUID != null && _sceneViewUUID.Length > 0)
                    stream.WriteBytes((uint)PropertyID.SceneViewUUID, _sceneViewUUID);
                if (_prefabName != null && _prefabName.Length > 0)
                    stream.WriteString((uint)PropertyID.PrefabName, _prefabName);
            }
    
            // Components
            if (_componentsModel != null)
                stream.WriteModel((uint)PropertyID.Components, _componentsModel, context);

            // Child Views
            if (_childViewsModel != null)
                stream.WriteModel((uint)PropertyID.ChildViews, _childViewsModel, context);
        }
        
        public void Read(ReadStream stream, StreamContext context) {
            // Loop through each property and deserialize
            uint propertyID;
            while (stream.ReadNextPropertyID(out propertyID)) {
                switch (propertyID) {
                    case 0:
                        stream.ReadModel(_metaModel, context);
                        break;
                    case (uint)PropertyID.SceneViewUUID:
                        byte[] sceneViewUUID = stream.ReadBytes();
                        bool sceneViewUUIDDidChange = (_sceneViewUUID != null && _sceneViewUUID.Length > 0) && !sceneViewUUID.SequenceEqual(_sceneViewUUID);
                        if (sceneViewUUIDDidChange) {
                            Debug.LogError("RealtimeViewModel scene UUID set by server, but this model already has a scene UUID. This is a bug!!");
                        }
                        _sceneViewUUID = sceneViewUUID;
                        break;
                    case (uint)PropertyID.PrefabName:
                        string prefabName = stream.ReadString();
                        bool prefabNameDidChange = prefabName != _prefabName;
                    
                        if (prefabNameDidChange && _componentsModel != null) {
                            Debug.LogError("RealtimeViewModel prefab name set by server, but this model is already associated with a prefab. This is a bug!!");
                            Debug.LogError("old: " + _prefabName + " new: " + prefabName);
                        }
                        _prefabName = prefabName;
                    
                        // Create components model and child views model if needed
                        CreateComponentsModelAndChildViewsModelIfNeeded(_prefabName);
    
                        // Deserialize cached components if needed
                        DeserializeCachedComponentsModelIfNeeded();

                        // Deserialize cached child views if needed
                        DeserializeCachedChildViewsModelIfNeeded();
                        break;
                    case (uint)PropertyID.Components:
                        if (_componentsModel != null) {
                            stream.ReadModel(_componentsModel, context);
                        } else {
                            if (context.fullModel) {
                                // Cache model buffer
                                _cachedComponentsModel = stream.ReadModelAsReadBuffer();
                                // Clear delta updates to apply on top
                                _cachedComponentsModelDeltaUpdates = new List<CachedDeltaUpdate>();
                            } else if (context.reliableChannel) {
                                // Record reliable delta updates to apply later if this model gets linked up to a RealtimeView.
                                _cachedComponentsModelDeltaUpdates.Add(new CachedDeltaUpdate(context.updateID, stream.ReadModelAsReadBuffer()));
                                if (_cachedComponentsModelDeltaUpdates.Count > 20) {
                                    Debug.LogWarning("Realtime: Received more than 20 reliable delta updates for a RealtimeViewModel that hasn't been connected to a RealtimeView. Either the prefab failed to instantiate, or this client doesn't have a RealtimeView in the scene with a matching UUID.");
                                }
                            }
                        }
                        break;
                    case (uint)PropertyID.ChildViews:
                        if (_childViewsModel != null) {
                            stream.ReadModel(_childViewsModel, context);
                        } else {
                            if (context.fullModel) {
                                // Cache model buffer
                                _cachedChildViewsModel = stream.ReadModelAsReadBuffer();
                                // Clear delta updates to apply on top
                                _cachedChildViewsModelDeltaUpdates = new List<CachedDeltaUpdate>();
                            } else if (context.reliableChannel) {
                                // Record reliable delta updates to apply later if this model gets linked up to a RealtimeView.
                                _cachedChildViewsModelDeltaUpdates.Add(new CachedDeltaUpdate(context.updateID, stream.ReadModelAsReadBuffer()));
                                if (_cachedChildViewsModelDeltaUpdates.Count > 20) {
                                    Debug.LogWarning("Realtime: Received more than 20 child view reliable delta updates for a RealtimeViewModel that hasn't been connected to a RealtimeView. Either the prefab failed to instantiate, or this client doesn't have a RealtimeView in the scene with a matching UUID.");
                                }
                            }
                        }
                        break;
                    default:
                        stream.SkipProperty();
                        break;
                }
            }
        }
    }
}
