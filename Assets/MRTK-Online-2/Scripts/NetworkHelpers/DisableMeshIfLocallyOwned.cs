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