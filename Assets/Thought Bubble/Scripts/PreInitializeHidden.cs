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
        private IEnumerator Start()
        {
            // Wait one frame so every child component's Start() (and the SDK's
            // deferred enable/disable registration handshake) has completed.
            yield return null;
            gameObject.SetActive(false);
            Debug.Log("Pre-initialized hidden object.");
        }
    }
}
