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