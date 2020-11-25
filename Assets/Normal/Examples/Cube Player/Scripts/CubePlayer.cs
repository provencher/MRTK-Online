#if NORMCORE

using UnityEngine;

namespace Normal.Realtime.Examples {
    public class CubePlayer : MonoBehaviour {
        public float speed = 5.0f;

        private RealtimeView      _realtimeView;
        private RealtimeTransform _realtimeTransform;

        private void Awake() {
            _realtimeView      = GetComponent<RealtimeView>();
            _realtimeTransform = GetComponent<RealtimeTransform>();
        }

        private void Update() {
            // If this CubePlayer prefab is not owned by this client, bail.
            if (!_realtimeView.isOwnedLocallySelf)
                return;

            // Make sure we own the transform so that RealtimeTransform knows to use this client's transform to synchronize remote clients.
            _realtimeTransform.RequestOwnership();

            // Grab the x/y input from WASD / a controller
            float x = Input.GetAxis("Horizontal");
            float y = Input.GetAxis("Vertical");

            // Apply to the transform
            Vector3 localPosition = transform.localPosition;
            localPosition.x += x * speed * Time.deltaTime;
            localPosition.y += y * speed * Time.deltaTime;
            transform.localPosition = localPosition;
        }
    }
}

#endif
