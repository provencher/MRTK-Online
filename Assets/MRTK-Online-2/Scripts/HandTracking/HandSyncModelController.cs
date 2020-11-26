//------------------------------------------------------------------------------ -
//MRTK - Quest - Online 2
//https ://github.com/provencher/MRTK-Quest-Online
//------------------------------------------------------------------------------ -
//
//MIT License
//
//Copyright(c) 2020 Eric Provencher
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files(the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions :
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
//------------------------------------------------------------------------------ -

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