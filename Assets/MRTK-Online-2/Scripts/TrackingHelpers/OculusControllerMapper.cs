using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace prvncher.MRTK_Online.TrackingHelpers
{
    public class OculusControllerMapper : MonoBehaviour
    {
        [SerializeField]
        Handedness _handedness = Handedness.None;

#if OCULUSINTEGRATION_PRESENT

        OVRCameraRig _cameraRig = null;

        bool _initialized = false;
        Transform _controllerAnchor = null;

        bool InitializeTrackingReference()
        {
            if (_initialized)
                return true;
            
            if (_handedness != Handedness.Left && _handedness != Handedness.Right)
                return false;

            _cameraRig = FindObjectOfType<OVRCameraRig>();
            _initialized = _cameraRig != null;
            if (_initialized)
            {
                _cameraRig.EnsureGameObjectIntegrity();
                _controllerAnchor = _handedness == Handedness.Left ? _cameraRig.leftControllerAnchor : _cameraRig.rightControllerAnchor;
            }

            return _initialized;
        }

        void Update()
        {
            if (!InitializeTrackingReference())
                return;

            transform.position = _controllerAnchor.position;
            transform.rotation = _controllerAnchor.rotation;
        }

#endif
    }
}