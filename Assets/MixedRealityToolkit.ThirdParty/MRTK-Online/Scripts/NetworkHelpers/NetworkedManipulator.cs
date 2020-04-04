using Microsoft.MixedReality.Toolkit.Experimental.UI;
using Microsoft.MixedReality.Toolkit.UI;
using Normal.Realtime;
using UnityEngine;

namespace prvncher.MRTK_Online.NetworkHelpers
{
    /// <summary>
    /// Bridge script between manipulator types and realtime transform from NormCore
    /// </summary>
    [RequireComponent(typeof(RealtimeTransform))]
    public class NetworkedManipulator : MonoBehaviour
    {
        private RealtimeTransform realtimeTransform = null;
        private ObjectManipulator objectManipulator = null;
        private ManipulationHandler manipulationHandler = null;

        private void Awake()
        {
            realtimeTransform = GetComponent<RealtimeTransform>();
            objectManipulator = GetComponent<ObjectManipulator>();
            manipulationHandler = GetComponent<ManipulationHandler>();
        }

        private void OnEnable()
        {
            if (objectManipulator != null)
            {
                objectManipulator.OnManipulationStarted.AddListener(OnManipulationStarted);
                objectManipulator.OnManipulationEnded.AddListener(OnManipulationEnded);

            }
            else if (manipulationHandler != null)
            {
                manipulationHandler.OnManipulationStarted.AddListener(OnManipulationStarted);
                manipulationHandler.OnManipulationEnded.AddListener(OnManipulationEnded);
            }
        }

        private void OnDisable()
        {
            if (objectManipulator != null)
            {
                objectManipulator.OnManipulationStarted.RemoveListener(OnManipulationStarted);
                objectManipulator.OnManipulationEnded.RemoveListener(OnManipulationEnded);

            }
            if (manipulationHandler != null)
            {
                manipulationHandler.OnManipulationStarted.RemoveListener(OnManipulationStarted);
                manipulationHandler.OnManipulationEnded.RemoveListener(OnManipulationEnded);
            }
        }

        private void OnManipulationStarted(ManipulationEventData manipulationEventData)
        {
            // In order to manipulate objects we need to request ownership
            realtimeTransform.RequestOwnership();
        }

        private void OnManipulationEnded(ManipulationEventData manipulationEventData)
        {
            // When not manipulating objects, we remove ownership
            if (realtimeTransform.isOwnedLocally)
            {
                realtimeTransform.ClearOwnership();
            }
        }
    }
}
