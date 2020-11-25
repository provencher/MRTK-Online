using Normal.Realtime;
using prvncher.MRTK_Online.TrackingHelpers;
using UnityEngine;

namespace prvncher.MRTK_Online.HandTracking
{
    public class HandSyncModelController : RealtimeComponent<HandSyncRealtimeModel>
    {
        [Header("Controller")]
        [SerializeField]
        GameObject rightControllerModelRoot = null;
        
        [SerializeField]
        GameObject leftControllerModelRoot = null;

        [Header("Hands")]
        [SerializeField]
        GameObject rightHandModelRoot = null;

        [SerializeField]
        GameObject leftHandModelRoot = null;
        
        [Header("Hands Sync Helpers")]
        [SerializeField]
        OculusHandTrackingSync rightHandSyncController = null;
        
        [SerializeField]
        OculusHandTrackingSync leftHandSyncController = null;

        bool _isOwnershipInitialized = false;
        
        void InitalizeLocalSystems()
        {
            if(_isOwnershipInitialized || !realtime.connected)
                return;
            
            _isOwnershipInitialized = true;
            
            leftHandSyncController.enabled = true;
            rightHandSyncController.enabled = true;

            leftControllerModelRoot.GetComponent<OculusControllerMapper>().enabled = true;
            rightControllerModelRoot.GetComponent<OculusControllerMapper>().enabled = true;

            foreach (var view in GetComponentsInChildren<RealtimeView>())
            {
                view.RequestOwnership();
            }
            foreach (var realtimeTransform in GetComponentsInChildren<RealtimeTransform>())
            {
                realtimeTransform.RequestOwnership();
            }
        }

        void Update()
        {
            if (isOwnedLocallyInHierarchy)
            {
                InitalizeLocalSystems();
                
                bool isHandTrackingActive = OVRPlugin.GetHandTrackingEnabled();
                model.isHandTrackingActive = isHandTrackingActive;
                model.isRightHandTrackingReliable = rightHandSyncController.isHandTrackingConfidenceHigh;
                model.isLeftHandTrackingReliable = leftHandSyncController.isHandTrackingConfidenceHigh;
                
                rightControllerModelRoot.SetActive(!isHandTrackingActive);
                rightHandModelRoot.SetActive(isHandTrackingActive);
                
                leftControllerModelRoot.SetActive(!isHandTrackingActive);
                leftHandModelRoot.SetActive(isHandTrackingActive);
            }
            else
            {
                bool isHandTrackingActive = model.isHandTrackingActive;
                
                rightControllerModelRoot.SetActive(!isHandTrackingActive);
                rightHandModelRoot.SetActive(isHandTrackingActive && model.isRightHandTrackingReliable);
                
                leftControllerModelRoot.SetActive(!isHandTrackingActive);
                leftHandModelRoot.SetActive(isHandTrackingActive && model.isLeftHandTrackingReliable);
            }
        }
    }
}