using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace prvncher.MRTK_Online.TrackingHelpers
{
    public class TrackedHeadPose : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cameraTransform = CameraCache.Main.transform;
            transform.position = cameraTransform.position;
            transform.rotation = cameraTransform.rotation;
        }
    }
}