using System.Collections;
using UnityEngine;

namespace ThoughtBubble
{
    /// <summary>
    /// Author this GameObject ENABLED in the scene, with this component on it.
    /// Meta Interaction SDK components (PokeInteractable, etc.) only register
    /// themselves for interaction at the end of their first Start(), and Start()
    /// never runs on an object that is inactive at scene load. So an object that
    /// starts disabled is "dead" the first time you enable it at runtime.
    ///
    /// This lets the object live for one frame so all child Start() methods run
    /// and the SDK completes its registration, then hides it. After that, normal
    /// SetActive(true)/SetActive(false) cycles work correctly.
    /// </summary>
    public class PreInitializeHidden : MonoBehaviour
    {
        [Tooltip("Other objects to deactivate at the end of the warm-up frame, " +
                 "before this object hides itself. Use this instead of putting a " +
                 "separate PreInitializeHidden on nested children: a single " +
                 "coroutine hides them in a fixed order, avoiding the race where a " +
                 "child's hide is skipped because its parent deactivated first.")]
        [SerializeField] private GameObject[] alsoHideAfterWarmup;

        private IEnumerator Start()
        {
            // Wait one frame so every child component's Start() (and the SDK's
            // deferred enable/disable registration handshake) has completed.
            yield return null;

            // Hide nested screens first, while this object is still active, so
            // their SetActive(false) is guaranteed to run.
            foreach (var go in alsoHideAfterWarmup)
            {
                if (go != null)
                    go.SetActive(false);
            }

            gameObject.SetActive(false);
            Debug.Log("Pre-initialized hidden object.");
        }
    }
}
