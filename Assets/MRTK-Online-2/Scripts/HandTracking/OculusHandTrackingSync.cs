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

using System;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Utilities;
using Normal.Realtime;
using UnityEngine;

namespace prvncher.MRTK_Online.HandTracking
{
    public class OculusHandTrackingSync : MonoBehaviour
    {
        [SerializeField]
        Handedness handedness = Handedness.None;

        [SerializeField]
        Transform rootTrackedTransform = null;

        public bool isHandTrackingConfidenceHigh = true;

#if OCULUSINTEGRATION_PRESENT

        Dictionary<OVRSkeleton.BoneId, Transform> boneIdMapping = new Dictionary<OVRSkeleton.BoneId, Transform>();

        OVRCameraRig ovrRig = null;
        OVRSkeleton skeletonReference = null;

        #region HandBone references

        private Transform handRoot = null;
        private Transform handWrist = null;

        private Transform handIndex1 = null;
        private Transform handIndex2 = null;
        private Transform handIndex3 = null;

        private Transform handMiddle1 = null;
        private Transform handMiddle2 = null;
        private Transform handMiddle3 = null;

        private Transform handRing1 = null;
        private Transform handRing2 = null;
        private Transform handRing3 = null;

        private Transform handPinky0 = null;
        private Transform handPinky1 = null;
        private Transform handPinky2 = null;
        private Transform handPinky3 = null;

        private Transform handThumb0 = null;
        private Transform handThumb1 = null;
        private Transform handThumb2 = null;
        private Transform handThumb3 = null;

        #endregion

        OVRSkeleton.SkeletonType GetSkeletonTypeFromHandedness(Handedness handedness)
        {
            switch (handedness)
            {
                case Handedness.Left:
                    return OVRSkeleton.SkeletonType.HandLeft;
                case Handedness.Right:
                    return OVRSkeleton.SkeletonType.HandRight;
                default:
                    return OVRSkeleton.SkeletonType.None;
            }
        }

        void Update()
        {
            if (!InitializeHandHiererachy())
            {
                Debug.LogError("Failed to init hierarchy for " + handedness);
                return;
            }
            
            if (!Application.isPlaying)
                return;

            InitializeRuntime();
            UpdateBones();
            isHandTrackingConfidenceHigh = skeletonReference.IsDataHighConfidence;
        }

        void InitializeRuntime()
        {
            if (!Application.isPlaying || ovrRig != null)
                return;

            ovrRig = FindObjectOfType<OVRCameraRig>();
            ovrRig.EnsureGameObjectIntegrity();

            foreach (var skeleton in ovrRig.GetComponentsInChildren<OVRSkeleton>())
            {
                if (skeleton.GetSkeletonType() != GetSkeletonTypeFromHandedness(handedness))
                    continue;

                skeletonReference = skeleton;
                break;
            }

            Debug.Log($"Hand Runtime {handedness} initilized");
        }

        void UpdateBones()
        {
            if (skeletonReference == null)
                return;

            foreach (var bone in skeletonReference.Bones)
            {
                if (boneIdMapping.TryGetValue(bone.Id, out Transform boneTransform))
                {
                    // Debug.Log(bone.Id + " - " + boneTransform);

                    // Root node gets worldspace transform matching
                    if (bone.Id == OVRSkeleton.BoneId.Hand_WristRoot)
                    {
                        boneTransform.position = bone.Transform.position;
                        boneTransform.rotation = bone.Transform.rotation;
                        continue;
                    }

                    if (boneTransform == null)
                        continue;

                    // Children bones get local rotation sync
                    boneTransform.localRotation = bone.Transform.localRotation;
                }
            }
        }

        private bool InitializeHandHiererachy()
        {
            // If we already have a valid hand root, proceed
            if (handRoot != null)
                return true;

            OVRSkeleton.SkeletonType skeletonType = GetSkeletonTypeFromHandedness(handedness);
            if (skeletonType != OVRSkeleton.SkeletonType.HandLeft && skeletonType != OVRSkeleton.SkeletonType.HandRight)
                return false;

            string handSignififer = skeletonType == OVRSkeleton.SkeletonType.HandLeft ? "l" : "r";
            string handStructure = "b_" + handSignififer;

            handRoot = transform;

            // Wrist
            string wristString = handStructure + "_wrist";
            handWrist = handRoot.Find(wristString);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_WristRoot, rootTrackedTransform);
            Debug.Log("Found: " + handWrist);

            // Index
            string indexString = handStructure + "_index";

            string indexString1 = indexString + "1";
            handIndex1 = handWrist.Find(indexString1);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Index1, handIndex1);
            Debug.Log("Found: " + handIndex1);

            string indexString2 = indexString + "2";
            handIndex2 = handIndex1.Find(indexString2);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Index2, handIndex2);

            string indexString3 = indexString + "3";
            handIndex3 = handIndex2.Find(indexString3);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Index3, handIndex3);

            // Middle
            string middleString = handStructure + "_middle";

            string middleString1 = middleString + "1";
            handMiddle1 = handWrist.Find(middleString1);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Middle1, handMiddle1);

            string middleString2 = middleString + "2";
            handMiddle2 = handMiddle1.Find(middleString2);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Middle2, handMiddle2);

            string middleString3 = middleString + "3";
            handMiddle3 = handMiddle2.Find(middleString3);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Middle3, handMiddle3);

            // Pinky
            string pinkyString = handStructure + "_pinky";

            string pinkyString0 = pinkyString + "0";
            handPinky0 = handWrist.Find(pinkyString0);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Pinky0, handPinky0);

            string pinkyString1 = pinkyString + "1";
            handPinky1 = handPinky0.Find(pinkyString1);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Pinky1, handPinky1);

            string pinkyString2 = pinkyString + "2";
            handPinky2 = handPinky1.Find(pinkyString2);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Pinky2, handPinky2);

            string pinkyString3 = pinkyString + "3";
            handPinky3 = handPinky2.Find(pinkyString3);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Pinky3, handPinky3);

            // Ring
            string ringString = handStructure + "_ring";

            string ringString1 = ringString + "1";
            handRing1 = handWrist.Find(ringString1);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Ring1, handRing1);

            string ringString2 = ringString + "2";
            handRing2 = handRing1.Find(ringString2);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Ring2, handRing2);

            string ringString3 = ringString + "3";
            handRing3 = handRing2.Find(ringString3);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Ring3, handRing3);

            // Thumb
            string thumbString = handStructure + "_thumb";

            string thumbString0 = thumbString + "0";
            handThumb0 = handWrist.Find(thumbString0);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Thumb0, handThumb0);

            string thumbString1 = thumbString + "1";
            handThumb1 = handThumb0.Find(thumbString1);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Thumb1, handThumb1);

            string thumbString2 = thumbString + "2";
            handThumb2 = handThumb1.Find(thumbString2);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Thumb2, handThumb2);

            string thumbString3 = thumbString + "3";
            handThumb3 = handThumb2.Find(thumbString3);
            boneIdMapping.Add(OVRSkeleton.BoneId.Hand_Thumb3, handThumb3);

            Debug.Log($"Hand {handedness} initilized");

            return true;
        }
#endif // If Oculus Integration Present
    }
}