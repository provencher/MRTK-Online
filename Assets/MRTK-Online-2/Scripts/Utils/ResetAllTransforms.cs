using System.Collections;
using System.Collections.Generic;
using prvncher.MRTK_Online.NetworkHelpers;
using UnityEngine;

namespace prvncher.MRTK_Online.Utils
{
    public class ResetAllTransforms : MonoBehaviour
    {
        List<NetworkedManipulator> networkedManipulators = new List<NetworkedManipulator>();

        private void Awake()
        {
            networkedManipulators.Clear();
            networkedManipulators.AddRange(FindObjectsOfType<NetworkedManipulator>());
        }

        public void ResetAllNetworkedManipulators()
        {
            foreach (var networkedManipulator in networkedManipulators)
            {
                networkedManipulator.ResetTransform();
            }
        }
    }
}