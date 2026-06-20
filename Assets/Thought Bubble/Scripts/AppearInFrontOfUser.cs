using UnityEngine;

namespace ThoughtBubble
{
    /// <summary>
    /// Repositions this object in front of the user whenever it is enabled.
    /// Attach to the root of a world-space UI (Canvas or panel of poke buttons).
    /// Place the GameObject inactive in the scene, then SetActive(true) to summon it.
    /// </summary>
    public class AppearInFrontOfUser : MonoBehaviour
    {
        [Tooltip("The user's head. Leave empty to auto-find the main camera (Meta's CenterEyeAnchor is tagged MainCamera).")]
        [SerializeField] private Transform _head;

        [Tooltip("Distance in meters to place the panel in front of the user. ~0.5-0.7 keeps it within poke reach.")]
        [SerializeField] private float _distance = 0.6f;

        [Tooltip("Raises (+) or lowers (-) the panel relative to eye height, in meters.")]
        [SerializeField] private float _verticalOffset = -0.1f;

        [Tooltip("Keep the panel level instead of tilting with the user's head pitch/roll.")]
        [SerializeField] private bool _keepUpright = true;

        [Tooltip("Re-follow the user every frame instead of placing once on enable.")]
        [SerializeField] private bool _followContinuously = false;

        private void OnEnable()
        {
            AppearInFront();
        }

        private void LateUpdate()
        {
            if (_followContinuously)
                AppearInFront();
        }

        /// <summary>
        /// Snaps the panel in front of the user and faces it toward them. Safe to call manually.
        /// </summary>
        public void AppearInFront()
        {
            Transform head = ResolveHead();
            if (head == null)
            {
                Debug.LogWarning($"{nameof(AppearInFrontOfUser)}: no head/camera found, cannot position '{name}'.", this);
                return;
            }

            // Direction the user is looking, optionally flattened so the panel stays level.
            Vector3 forward = head.forward;
            if (_keepUpright)
            {
                forward.y = 0f;
                // Guard against the user looking straight up or down.
                if (forward.sqrMagnitude < 0.0001f)
                    forward = transform.position - head.position;
                forward.y = 0f;
            }
            forward.Normalize();

            Vector3 position = head.position + forward * _distance;
            position.y += _verticalOffset;
            transform.position = position;

            // Align the panel's forward with the look direction so a world-space Canvas reads correctly.
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private Transform ResolveHead()
        {
            if (_head != null)
                return _head;

            Camera main = Camera.main;
            if (main != null)
                _head = main.transform;

            return _head;
        }
    }
}
