using System.Collections;
using System.Collections.Generic;
using Normal.Realtime;
using UnityEngine;

namespace prvncher.MRTK_Online.NetworkHelpers
{
    [RequireComponent(typeof(Renderer))]
    public class DisableMeshIfLocallyOwned : MonoBehaviour
    {
        [SerializeField]
        private RealtimeView _realtimeView = null;

        private void Update()
        {
            if (_realtimeView != null && _realtimeView.realtime.connected)
            {
                GetComponent<Renderer>().enabled = !_realtimeView.isOwnedLocally;
                enabled = false;
            }
        }
    }
}