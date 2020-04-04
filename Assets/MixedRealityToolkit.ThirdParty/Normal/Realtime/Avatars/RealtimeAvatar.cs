using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Normal.Realtime.Serialization;
using Normal.Utility;

namespace Normal.Realtime {
    [ExecutionOrder(-95)] // Make sure our Update() runs before the default to so that the avatar positions are as up to date as possible when everyone else's Update() runs.
    public class RealtimeAvatar : RealtimeComponent {
        // Local Player
        [Serializable]
        public class LocalPlayer {
            public Transform root;
            public Transform head;
            public Transform leftHand;
            public Transform rightHand;
        }
        public  LocalPlayer  localPlayer { get { return _localPlayer; } set { SetLocalPlayer(value); } }
#pragma warning disable 0649 // Disable variable is never assigned to warning.
        private LocalPlayer _localPlayer;
#pragma warning restore 0649

        // Device Type
        public enum DeviceType : uint {
            Unknown = 0,
            OpenVR  = 1,
            Oculus  = 2,
        }
        public DeviceType deviceType { get { return DeviceTypeFromUInt(_model.deviceType); } set { _model.deviceType = DeviceTypeToUInt(value); } }

        // Prefab
        public Transform head      { get { return _head;      } }
        public Transform leftHand  { get { return _leftHand;  } }
        public Transform rightHand { get { return _rightHand; } }
#pragma warning disable 0649 // Disable variable is never assigned to warning.
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightHand;
#pragma warning restore 0649

        // Serialization
        private RealtimeAvatarModel _model;
        public  RealtimeAvatarModel  model { get { return _model; } set { SetModel(value); } }

        private RealtimeAvatarManager _realtimeAvatarManager;

        private static List<XRNodeState> _nodeStates = new List<XRNodeState>();

        void Start() {
            // Register with RealtimeAvatarManager
            try {
                _realtimeAvatarManager = realtime.GetComponent<RealtimeAvatarManager>();
                _realtimeAvatarManager._RegisterAvatar(realtimeView.ownerID, this);
            } catch (NullReferenceException) {
                Debug.LogError("RealtimeAvatar failed to register with RealtimeAvatarManager component. Was this avatar prefab instantiated by RealtimeAvatarManager?");
            }
        }

        void OnDestroy() {
            // Unregister with RealtimeAvatarManager
            if (_realtimeAvatarManager != null)
                _realtimeAvatarManager._UnregisterAvatar(this);

            // Unregister for events
            localPlayer = null;
        }
    
        void FixedUpdate() {
            UpdateAvatarTransformsForLocalPlayer();
        }
    
        void Update() {
            UpdateAvatarTransformsForLocalPlayer();
        }
    
        void LateUpdate() {
            UpdateAvatarTransformsForLocalPlayer();
        }

        void SetModel(RealtimeAvatarModel model) {
            if (model == _model)
                return;

            if (_model != null) {
                _model.activeStateChanged -= ActiveStateChanged;
            }

            _model = model;

            if (_model != null) {
                _model.activeStateChanged += ActiveStateChanged;
            }
        }

        void SetLocalPlayer(LocalPlayer localPlayer) {
            if (localPlayer == _localPlayer)
                return;

            _localPlayer = localPlayer;
    
            if (_localPlayer != null) {
                // TODO: Technically this shouldn't be needed. The RealtimeViewModel is created and locked to a user. The owner of that should progagate to children...
                RealtimeTransform      rootRealtimeTransform =                                 GetComponent<RealtimeTransform>();
                RealtimeTransform      headRealtimeTransform =      _head != null ?      _head.GetComponent<RealtimeTransform>() : null;
                RealtimeTransform  leftHandRealtimeTransform =  _leftHand != null ?  _leftHand.GetComponent<RealtimeTransform>() : null;
                RealtimeTransform rightHandRealtimeTransform = _rightHand != null ? _rightHand.GetComponent<RealtimeTransform>() : null;
                if (     rootRealtimeTransform != null)      rootRealtimeTransform.RequestOwnership();
                if (     headRealtimeTransform != null)      headRealtimeTransform.RequestOwnership();
                if ( leftHandRealtimeTransform != null)  leftHandRealtimeTransform.RequestOwnership();
                if (rightHandRealtimeTransform != null) rightHandRealtimeTransform.RequestOwnership();
            }
        }

        void ActiveStateChanged(RealtimeAvatarModel model, uint activeState) {
            if (model != _model)
                return;

            //   if (_head != null)      _head.gameObject.SetActive(model.headActive); // I deprecated this because it will cause RealtimeAvatarVoice to not run when the head isn't tracking...
            if ( _leftHand != null)  _leftHand.gameObject.SetActive(model.leftHandActive);
            if (_rightHand != null) _rightHand.gameObject.SetActive(model.rightHandActive);
        }

        void UpdateAvatarTransformsForLocalPlayer() {
            // Make sure this avatar is a local player
            if (_localPlayer == null)
                return;

            // Flags to fetch XRNode position/rotation state
            bool updateHeadWithXRNode      = false;
            bool updateLeftHandWithXRNode  = false;
            bool updateRightHandWithXRNode = false;

            // Root
            if (_localPlayer.root != null) {
                transform.position   = _localPlayer.root.position;
                transform.rotation   = _localPlayer.root.rotation;
                transform.localScale = _localPlayer.root.localScale;
            }

            // Head
            if (_localPlayer.head != null) {
                _model.headActive = _localPlayer.head.gameObject.activeSelf;

                _head.position = _localPlayer.head.position;
                _head.rotation = _localPlayer.head.rotation;
            } else {
                updateHeadWithXRNode = true;
            }

            // Left Hand
            if (_leftHand != null) {
                if (_localPlayer.leftHand != null) {
                    _model.leftHandActive = _localPlayer.leftHand.gameObject.activeSelf;
                
                    _leftHand.position = _localPlayer.leftHand.position;
                    _leftHand.rotation = _localPlayer.leftHand.rotation;
                } else {
                    updateLeftHandWithXRNode = true;
                }
            }

            // Right Hand
            if (_rightHand != null) {
                if (_localPlayer.rightHand != null) {
                    _model.rightHandActive = _localPlayer.rightHand.gameObject.activeSelf;
                
                    _rightHand.position = _localPlayer.rightHand.position;
                    _rightHand.rotation = _localPlayer.rightHand.rotation;
                } else {
                    updateRightHandWithXRNode = true;
                }
            }

            // Update head/hands using XRNode APIs if needed
            if (updateHeadWithXRNode || updateLeftHandWithXRNode || updateRightHandWithXRNode) {
                _nodeStates.Clear();
                InputTracking.GetNodeStates(_nodeStates);

                bool      headActive = false;
                bool  leftHandActive = false;
                bool rightHandActive = false;

                foreach (XRNodeState nodeState in _nodeStates) {
                    if (nodeState.nodeType == XRNode.Head && updateHeadWithXRNode) {
                        headActive = nodeState.tracked;

                        Vector3 position;
                        if (nodeState.TryGetPosition(out position))
                            _head.localPosition = position;

                        Quaternion rotation;
                        if (nodeState.TryGetRotation(out rotation))
                            _head.localRotation = rotation;
                    } else if (nodeState.nodeType == XRNode.LeftHand && updateLeftHandWithXRNode) {
                        leftHandActive = nodeState.tracked;

                        Vector3 position;
                        if (nodeState.TryGetPosition(out position))
                            _leftHand.localPosition = position;

                        Quaternion rotation;
                        if (nodeState.TryGetRotation(out rotation))
                            _leftHand.localRotation = rotation;
                    } else if (nodeState.nodeType == XRNode.RightHand && updateRightHandWithXRNode) {
                        rightHandActive = nodeState.tracked;

                        Vector3 position;
                        if (nodeState.TryGetPosition(out position))
                            _rightHand.localPosition = position;

                        Quaternion rotation;
                        if (nodeState.TryGetRotation(out rotation))
                            _rightHand.localRotation = rotation;
                    }
                }

                if (     updateHeadWithXRNode) _model.headActive      =      headActive;
                if ( updateLeftHandWithXRNode) _model.leftHandActive  =  leftHandActive;
                if (updateRightHandWithXRNode) _model.rightHandActive = rightHandActive;
            }
        }

        private static uint DeviceTypeToUInt(DeviceType deviceType) {
            return (uint)deviceType;
        }
        
        private static DeviceType DeviceTypeFromUInt(uint deviceType) {
            switch (deviceType) {
                case 1:
                    return DeviceType.OpenVR;
                case 2:
                    return DeviceType.Oculus;
                default:
                    return DeviceType.Unknown;
            }
        }
    }

    public class RealtimeAvatarModel : IModel {
        public bool headActive      { get { return (activeState & (uint)ActiveStateFlags.HeadActive)      != 0; } set { SetHeadActive(value);      } }
        public bool leftHandActive  { get { return (activeState & (uint)ActiveStateFlags.LeftHandActive)  != 0; } set { SetLeftHandActive(value);  } }
        public bool rightHandActive { get { return (activeState & (uint)ActiveStateFlags.RightHandActive) != 0; } set { SetRightHandActive(value); } }

        [Flags]
        private enum ActiveStateFlags : uint {
            Default         = 0,
            HeadActive      = 1 << 0,
            LeftHandActive  = 1 << 1,
            RightHandActive = 1 << 2,
        }
        private uint _activeState = 0;
        private uint  activeState {
            get { return _cache.LookForValueInCache(_activeState, entry => entry.activeStateSet, entry => entry.activeState); }
            set { _cache.UpdateLocalCache(entry => { entry.activeStateSet = true; entry.activeState = value; return entry; }); FireActiveStateChanged(); }
        }
        public delegate void ActiveStateChanged(RealtimeAvatarModel model, uint activeState);
        public event ActiveStateChanged activeStateChanged;

        private uint _deviceType = 0;
        public  uint  deviceType {
            get { return _cache.LookForValueInCache(_deviceType, entry => entry.deviceTypeSet, entry => entry.deviceType);   }
            set { _cache.UpdateLocalCache(entry => { entry.deviceTypeSet = true; entry.deviceType = value; return entry; }); }
        }

        // Serialization
        private enum Properties : uint {
            ActiveState = 1,
            DeviceType  = 2,
        }

        private struct LocalCacheEntry {
            public bool activeStateSet;
            public uint activeState;
            public bool deviceTypeSet;
            public uint deviceType;
        }
        private LocalChangeCache<LocalCacheEntry> _cache;

        public RealtimeAvatarModel() {
            _cache = new LocalChangeCache<LocalCacheEntry>();
        }

        // Properties
        void SetHeadActive(bool active) {
            if (active == headActive)
                return;

            if (active)
                activeState |=   (uint)ActiveStateFlags.HeadActive;
            else
                activeState &= ~((uint)ActiveStateFlags.HeadActive);
        }

        void SetLeftHandActive(bool active) {
            if (active == leftHandActive)
                return;

            if (active)
                activeState |=   (uint)ActiveStateFlags.LeftHandActive;
            else
                activeState &= ~((uint)ActiveStateFlags.LeftHandActive);
        }

        void SetRightHandActive(bool active) {
            if (active == rightHandActive)
                return;

            if (active)
                activeState |=   (uint)ActiveStateFlags.RightHandActive;
            else
                activeState &= ~((uint)ActiveStateFlags.RightHandActive);
        }

        // Events
        void FireActiveStateChanged() {
            if (activeStateChanged != null) {
                try {
                    activeStateChanged(this, activeState);
                } catch (Exception exception) {
                    Debug.LogException(exception);
                }
            }
        }

        // Serialization
        public int WriteLength(StreamContext context) {
            int length = 0;

            if (context.fullModel) {
                // Flatten cache
                _activeState = activeState;
                _deviceType  = deviceType;
                _cache.Clear();

                // Active state
                length += WriteStream.WriteVarint32Length((uint)Properties.ActiveState, _activeState);

                // Device type
                length += WriteStream.WriteVarint32Length((uint)Properties.DeviceType,  _deviceType);
            } else {
                // Active state
                if (context.reliableChannel) {
                    LocalCacheEntry entry = _cache.localCache;
                    if (entry.activeStateSet)
                        length += WriteStream.WriteVarint32Length((uint)Properties.ActiveState, entry.activeState);
                    if (entry.deviceTypeSet)
                        length += WriteStream.WriteVarint32Length((uint)Properties.DeviceType,  entry.deviceType);
                }
            }

            return length;
        }

        public void Write(WriteStream stream, StreamContext context) {
            if (context.fullModel) {
                // Active state
                stream.WriteVarint32((uint)Properties.ActiveState, _activeState);

                // Device type
                stream.WriteVarint32((uint)Properties.DeviceType, _deviceType);
            } else {
                // Active state
                if (context.reliableChannel) {
                    // If we're going to send an update. Push the cache to inflight.
                    LocalCacheEntry entry = _cache.localCache;
                    if (entry.activeStateSet || entry.deviceTypeSet)
                        _cache.PushLocalCacheToInflight(context.updateID);
                    
                    if (entry.activeStateSet)
                        stream.WriteVarint32((uint)Properties.ActiveState, entry.activeState);
                    if (entry.deviceTypeSet)
                        stream.WriteVarint32((uint)Properties.DeviceType,  entry.deviceType);
                }
            }
        }

        public void Read(ReadStream stream, StreamContext context) {
            // Remove from in-flight
            if (context.deltaUpdatesOnly && context.reliableChannel)
                _cache.RemoveUpdateFromInflight(context.updateID);

            // Read properties
            uint propertyID;
            while (stream.ReadNextPropertyID(out propertyID)) {
                switch (propertyID) {
                    case (uint)Properties.ActiveState:
                        _activeState = stream.ReadVarint32();
                        FireActiveStateChanged();
                        break;
                    case (uint)Properties.DeviceType:
                        _deviceType = stream.ReadVarint32();
                        break;
                    default:
                        stream.SkipProperty();
                        break;
                }
            }
        }
    }
}