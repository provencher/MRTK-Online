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

        private Vector3 startPosition = Vector3.zero;
        private Quaternion startRotation = Quaternion.identity;
        private Vector3 startScale = Vector3.zero;

        private Rigidbody rigidbody = null;

        [SerializeField]
        [Tooltip("If true, realtime transform will extrapolate physics objects.")]
        private bool shouldExtrapolateIfRigidbody = true;

        [SerializeField]
        [Tooltip("Delay before ownership is reset after ownership is changed.")]
        private float ownershipTimeOutTime = 1f;

        private bool isManipulating = false;
        private float lastOwnershipChangeTime = 0f;

        private bool hasOwnership = false;

        private void Awake()
        {
            realtimeTransform = GetComponent<RealtimeTransform>();
            objectManipulator = GetComponent<ObjectManipulator>();
            manipulationHandler = GetComponent<ManipulationHandler>();
            rigidbody = GetComponent<Rigidbody>();

            startPosition = transform.localPosition;
            startRotation = transform.localRotation;
            startScale = transform.localScale;

            if (shouldExtrapolateIfRigidbody && rigidbody != null)
            {
                realtimeTransform.extrapolation = true;
            }
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

        private void Update()
        {
            if (!CheckOwnership()) return;

            // If we just reset objects, we should wait before affecting ownership
            if (Time.time - lastOwnershipChangeTime < 1f) return;

            // If object is not being manipulated, or it is at rest, clear ownership
            if (!isManipulating && (rigidbody == null || rigidbody.velocity == Vector3.zero))
            {
                realtimeTransform.ClearOwnership();
            }
        }

        private bool CheckOwnership()
        {
            if (hasOwnership != realtimeTransform.isOwnedLocally)
            {
                hasOwnership = realtimeTransform.isOwnedLocally;
                ResetOwnershipTime();
            }
            return hasOwnership;
        }

        /// <summary>
        /// Resets the transform and rigidbody to initial conditions
        /// </summary>
        public void ResetTransform()
        {
            if (realtimeTransform != null)
            {
                realtimeTransform.RequestOwnership();

                // Reset Transform
                transform.localPosition = startPosition;
                transform.localRotation = startRotation;
                transform.localScale = startScale;

                if (rigidbody != null)
                {
                    rigidbody.angularVelocity = Vector3.zero;
                    rigidbody.velocity = Vector3.zero;
                }
            }
        }

        private void ResetOwnershipTime()
        {
            lastOwnershipChangeTime = Time.time;
        }

        private void OnManipulationStarted(ManipulationEventData manipulationEventData)
        {
            // In order to manipulate objects we need to request ownership
            realtimeTransform.RequestOwnership();
            isManipulating = true;
        }

        private void OnManipulationEnded(ManipulationEventData manipulationEventData)
        {
            isManipulating = false;
        }
    }
}
