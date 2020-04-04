using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Serialization;

namespace Normal.Realtime {
    [RequireComponent(typeof(Realtime))]
    public class RealtimeAvatarManager : MonoBehaviour {
#pragma warning disable 0649 // Disable variable is never assigned to warning.
        [FormerlySerializedAs("_avatarPrefab")]
        [SerializeField] private GameObject _localAvatarPrefab;
        [SerializeField] private RealtimeAvatar.LocalPlayer _localPlayer;
#pragma warning restore 0649

        public GameObject localAvatarPrefab { get { return _localAvatarPrefab; } set { SetLocalAvatarPrefab(value); } }

        public RealtimeAvatar                  localAvatar { get; private set; }
        public Dictionary<int, RealtimeAvatar> avatars     { get; private set; }
        
        public delegate void AvatarCreatedDestroyed(RealtimeAvatarManager avatarManager, RealtimeAvatar avatar, bool isLocalAvatar);
        public event AvatarCreatedDestroyed avatarCreated;
        public event AvatarCreatedDestroyed avatarDestroyed;
        
        private Realtime _realtime;

        void Awake() {
            _realtime = GetComponent<Realtime>();
            _realtime.didConnectToRoom += DidConnectToRoom;

            if (_localPlayer == null)
                _localPlayer = new RealtimeAvatar.LocalPlayer();

            avatars = new Dictionary<int, RealtimeAvatar>();
        }

        private void OnEnable() {
            // Create avatar if we're already connected
            if (_realtime.connected)
                CreateAvatarIfNeeded();
        }

        private void OnDisable() {
            // Destroy avatar if needed
            DestroyAvatarIfNeeded();
        }

        void OnDestroy() {
            _realtime.didConnectToRoom -= DidConnectToRoom;
        }

        void DidConnectToRoom(Realtime room) {
            if (!gameObject.activeInHierarchy || !enabled)
                return;

            // Create avatar
            CreateAvatarIfNeeded();
        }

        public static RealtimeAvatar.DeviceType GetRealtimeAvatarDeviceTypeForLocalPlayer() {
            switch (XRSettings.loadedDeviceName) {
                case "OpenVR":
                    return RealtimeAvatar.DeviceType.OpenVR;
                case "Oculus":
                    return RealtimeAvatar.DeviceType.Oculus;
                default:
                    return RealtimeAvatar.DeviceType.Unknown;
            }
        }

        public void _RegisterAvatar(int clientID, RealtimeAvatar avatar) {
            if (avatars.ContainsKey(clientID)) {
                Debug.LogError("RealtimeAvatar registered more than once for the same clientID (" + clientID + "). This is a bug!");
            }
            avatars[clientID] = avatar;
            
            // Fire event
            if (avatarCreated != null) {
                try {
                    avatarCreated(this, avatar, clientID == _realtime.clientID);
                } catch (System.Exception exception) {
                    Debug.LogException(exception);
                }
            }
        }

        public void _UnregisterAvatar(RealtimeAvatar avatar) {
            bool isLocalAvatar = false;
            
            List<KeyValuePair<int, RealtimeAvatar>> matchingAvatars = avatars.Where(keyValuePair => keyValuePair.Value == avatar).ToList();
            foreach (KeyValuePair<int, RealtimeAvatar> matchingAvatar in matchingAvatars) {
                int avatarClientID = matchingAvatar.Key;
                avatars.Remove(avatarClientID);
                
                isLocalAvatar = isLocalAvatar || avatarClientID == _realtime.clientID;
            }
            
            // Fire event
            if (avatarDestroyed != null) {
                try {
                    avatarDestroyed(this, avatar, isLocalAvatar);
                } catch (System.Exception exception) {
                    Debug.LogException(exception);
                }
            }
        }
        
        private void SetLocalAvatarPrefab(GameObject localAvatarPrefab) {
            if (localAvatarPrefab == _localAvatarPrefab)
                return;
            
            _localAvatarPrefab = localAvatarPrefab;
            
            // Replace the existing avatar if we've already instantiated the old prefab.
            if (localAvatar != null) {
                DestroyAvatarIfNeeded();
                CreateAvatarIfNeeded();
            }
        }
        
        public void CreateAvatarIfNeeded() {
            if (!_realtime.connected) {
                Debug.LogError("RealtimeAvatarManager: Unable to create avatar. Realtime is not connected to a room.");
                return;
            }

            if (localAvatar != null)
                return;

            if (_localAvatarPrefab == null) {
                Debug.LogWarning("Realtime Avatars local avatar prefab is null. No avatar prefab will be instantiated for the local player.");
                return;
            }

            GameObject avatarGameObject = Realtime.Instantiate(_localAvatarPrefab.name, true, true, true, _realtime);
            if (avatarGameObject == null) {
                Debug.LogError("RealtimeAvatarManager: Failed to instantiate RealtimeAvatar prefab for the local player.");
                return;
            }

            localAvatar = avatarGameObject.GetComponent<RealtimeAvatar>();
            if (avatarGameObject == null) {
                Debug.LogError("RealtimeAvatarManager: Successfully instantiated avatar prefab, but could not find the RealtimeAvatar component.");
                return;
            }

            localAvatar.localPlayer = _localPlayer;
            localAvatar.deviceType = GetRealtimeAvatarDeviceTypeForLocalPlayer();
        }

        public void DestroyAvatarIfNeeded() {
            if (localAvatar == null)
                return;

            Realtime.Destroy(localAvatar.gameObject);

            localAvatar = null;
        }
    }
}
