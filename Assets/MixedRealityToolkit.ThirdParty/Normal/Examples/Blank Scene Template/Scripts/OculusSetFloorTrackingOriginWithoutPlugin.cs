using System.Runtime.InteropServices;
using UnityEngine;

namespace Normal.Realtime.Utility {
    public class OculusSetFloorTrackingOriginWithoutPlugin : MonoBehaviour {
        private enum Bool {
            False = 0,
            True
        }
    
        private enum TrackingOrigin {
            EyeLevel = 0,
            FloorLevel = 1,
            Count,
        }
    
        [DllImport("OVRPlugin", CallingConvention = CallingConvention.Cdecl)]
        private static extern Bool ovrp_SetTrackingOriginType(TrackingOrigin originType);
    
        private static bool __stopTryingToSetTrackingOrigin = false;
    
        void Update() {
            if (__stopTryingToSetTrackingOrigin)
                return;
    
            try {
                __stopTryingToSetTrackingOrigin = ovrp_SetTrackingOriginType(TrackingOrigin.FloorLevel) == Bool.True;
            } catch {
                // Plugin probably doesn't exist. Give up.
                __stopTryingToSetTrackingOrigin = true;
            }
        }
    }
}
