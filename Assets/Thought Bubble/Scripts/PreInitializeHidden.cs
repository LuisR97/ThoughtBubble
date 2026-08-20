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

        [Tooltip("Frames to wait before hiding. Covers frame-based work: Start() running " +
                 "and the SDK completing its registration handshake. One is sufficient for " +
                 "that; no amount of seconds can substitute, because Start() is dispatched " +
                 "per frame, not per second.")]
        public int nullFrames = 1;

        [Tooltip("Extra seconds to wait after the frame count. Covers time-based work — " +
                 "chiefly InteractableColorVisual's colour fade, which accumulates " +
                 "Time.deltaTime until it reaches ColorTime. Frames are a bad proxy for " +
                 "this: at an uncapped editor framerate a dozen frames can elapse in 20ms " +
                 "and cut the fade short, leaving buttons stuck at a partial colour. " +
                 "Set to the longest ColorTime in use, plus a frame, plus margin.")]
        public float warmupSeconds = 0f;

        private IEnumerator Start()
        {
            // Wait number of frames so every child component's Start() (and the SDK's
            // deferred enable/disable registration handshake) has completed.
            for (int i = 0; i < nullFrames; i++)
            {
                yield return null;
            }

            // Then wait out any time-based work that the frame count cannot guarantee.
            // WaitForSeconds (scaled, not Realtime) deliberately matches the Time.deltaTime
            // the colour fade itself uses, so the two stay in step if timeScale changes.
            if (warmupSeconds > 0f)
            {
                yield return new WaitForSeconds(warmupSeconds);
            }

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
