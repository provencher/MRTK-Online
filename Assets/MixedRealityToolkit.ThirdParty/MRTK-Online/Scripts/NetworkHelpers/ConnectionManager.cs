using System.Linq;
using Normal.Realtime;
using UnityEngine;

namespace prvncher.MRTK_Online.NetworkHelpers
{
    [RequireComponent(typeof(NetworkReconnect))]
    public class ConnectionManager : MonoBehaviour
    {
        public static string CurrentRoomName { get; private set; } = "Test Room";

        [SerializeField]
        private string defaultRoomName = CurrentRoomName;

        private NetworkReconnect networkReconnect;

        private bool isInitialized = false;
        private bool hasLostFocusOnce = false;
        private bool hasPausedOnce = false;

        private void Awake()
        {
            networkReconnect = GetComponent<NetworkReconnect>();
        }

        private void Start()
        {
            ConnectToRoom(defaultRoomName);
        }

        public void ConnectToRoom(string roomName)
        {
            Initialize(roomName);
        }

        private void Initialize(string roomName)
        {
            Realtime instance = Realtime.instances.SingleOrDefault();
            if (instance == null)
            {
                Debug.LogError("No realtime isntances found. Cannot initiate connect.");
                return;
            }
            networkReconnect.Inject(instance);
            CurrentRoomName = roomName;
            isInitialized = true;
            instance.Connect(roomName);
        }

        private void OnApplicationPause(bool pause)
        {
            if (!isInitialized) return;

            if (pause)
            {
                networkReconnect.Pause();
                hasPausedOnce = true;
            }
            else if (hasPausedOnce)
            {
                networkReconnect.Resume();
            }
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!isInitialized) return;

            if (!focus)
            {
                networkReconnect.Pause();
                hasLostFocusOnce = true;
            }
            else if (hasLostFocusOnce)
            {
                networkReconnect.Resume();
            }
        }
    }
}
