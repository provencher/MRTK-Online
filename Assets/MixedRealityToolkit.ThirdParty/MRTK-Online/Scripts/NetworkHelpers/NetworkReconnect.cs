using System.Collections;
using UnityEngine;
using Normal.Realtime;

namespace prvncher.MRTK_Online.NetworkHelpers
{

    /// <summary>
    /// Gist taken from:
    /// https://gist.github.com/Godatplay/48cc2105a4a9710721a9f4869abe1915
    /// </summary>
    public class NetworkReconnect : MonoBehaviour
    {
        private Realtime realtime;
        private bool isWaitingForDisconnect = false;

        public void Inject( Realtime realtime)
        {
            this.realtime = realtime;
        }

        private void Init()
        {
            isWaitingForDisconnect = false;
        }

        public void Pause()
        {
            if (realtime.connected || realtime.connecting)
            {
                ReconnectRealtimeUponDisconnect();
                StartCoroutine(WaitThenCancelReconnect());
            }
        }

        public void Resume()
        {
            StopCoroutine(WaitThenCancelReconnect());
            if (!isWaitingForDisconnect)
                ReconnectRealtimeUponDisconnect();

            if (!realtime.connected && !realtime.connecting)
                ConnectToRoom(realtime);
        }

        private void ReconnectRealtimeUponDisconnect()
        {
            if (!isWaitingForDisconnect)
            {
                realtime.didDisconnectFromRoom += ConnectToRoom;
                isWaitingForDisconnect = true;
            }
        }
        public void CancelReconnectRealtimeUponDisconnect()
        {
            if (isWaitingForDisconnect)
            {
                realtime.didDisconnectFromRoom -= ConnectToRoom;
                isWaitingForDisconnect = false;
            }
        }

        private void ConnectToRoom(Realtime realtime)
        {
            if (!realtime.connected && !realtime.connecting)
            {
                realtime.Connect(ConnectionManager.CurrentRoomName);
            }
        }

        private IEnumerator WaitThenCancelReconnect()
        {
            yield return new WaitForSecondsRealtime(60f);
            CancelReconnectRealtimeUponDisconnect();
        }
    }
}