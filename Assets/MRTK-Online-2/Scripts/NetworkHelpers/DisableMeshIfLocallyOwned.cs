using Normal.Realtime;
using UnityEngine;

namespace prvncher.MRTK_Online.NetworkHelpers
{
    [RequireComponent(typeof(Renderer))]
    public class DisableMeshIfLocallyOwned : MonoBehaviour
    {
        [SerializeField]
        private RealtimeView _realtimeView = null;

        Renderer _render = null;
        
        void Awake()
        {
            _render = GetComponent<Renderer>();
        }

        private void Update()
        {
            if (_realtimeView != null && _realtimeView.realtime.connected)
            {
                _render.enabled = !_realtimeView.isOwnedLocallyInHierarchy;
                enabled = false;
            }
        }
    }
}