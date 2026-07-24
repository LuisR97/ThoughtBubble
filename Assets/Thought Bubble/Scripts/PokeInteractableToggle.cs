using System.Collections;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Put this on a poke button (the object that has the PokeInteractable). Lets an
/// external script enable/disable the button at runtime.
///
/// Disabling the PokeInteractable puts it in the Disabled state, so the button's
/// InteractableColorVisual shows its grey "disabled" colour automatically, and the
/// button can no longer be poked. Enabling restores both. We only toggle the
/// PokeInteractable component (not the GameObject), so the button stays visible and
/// its visual stays active to react to the state change.
/// </summary>
public class PokeInteractableToggle : MonoBehaviour
{
    [Tooltip("The PokeInteractable to toggle. Auto-found on this object / its children if left empty.")]
    [SerializeField] private PokeInteractable _pokeInteractable;

    [Tooltip("If true, the button starts disabled/greyed (applied after it has registered).")]
    [SerializeField] private bool _startDisabled = false;

    /// <summary>True if the button is currently enabled (pokeable, not greyed).</summary>
    public bool IsEnabled => _pokeInteractable != null && _pokeInteractable.enabled;

    // Editor convenience: auto-assign the reference when the component is first added.
    private void Reset() => FindInteractable();

    private void Awake()
    {
        if (_pokeInteractable == null)
            FindInteractable();
        if (_pokeInteractable == null)
            Debug.LogError($"{nameof(PokeInteractableToggle)}: no PokeInteractable found on '{name}' or its children.", this);
    }

    // Re-apply the start-disabled state every time this button becomes active — e.g.
    // each time its menu is shown via SetActive(true). Using OnEnable instead of a
    // one-shot Start() does two things:
    //   1. Resets the button to disabled on every menu open, so a prior session's
    //      Enable() calls don't leak into the next open.
    //   2. Survives the scene warm-up race. PreInitializeHidden also waits one frame
    //      then hides its menu; if it wins that race and deactivates us before the
    //      deferred SetEnabled(false) runs, the coroutine is killed. Because this is
    //      in OnEnable, it simply runs again on the next real activation.
    private void OnEnable()
    {
        if (_startDisabled)
            StartCoroutine(ApplyStartDisabled());
    }

    private IEnumerator ApplyStartDisabled()
    {
        // Wait one frame so the PokeInteractable completes its own enable/registration
        // handshake first. Disabling it in the same frame it activates can leave it
        // "dead" instead of cleanly disabled.
        yield return null;
        SetEnabled(false);
    }

    /// <summary>Enable (ungrey, pokeable) or disable (grey, not pokeable) the button.</summary>
    public void SetEnabled(bool value)
    {
        if (_pokeInteractable != null)
            _pokeInteractable.enabled = value;
    }

    /// <summary>Disable the button: greys it out and blocks poking.</summary>
    public void Disable() => SetEnabled(false);

    /// <summary>Enable the button: removes the grey and allows poking.</summary>
    public void Enable() => SetEnabled(true);

    /// <summary>Flip between enabled and disabled.</summary>
    public void Toggle()
    {
        if (_pokeInteractable != null)
            _pokeInteractable.enabled = !_pokeInteractable.enabled;
    }

    private void FindInteractable()
    {
        _pokeInteractable = GetComponent<PokeInteractable>();
        if (_pokeInteractable == null)
            _pokeInteractable = GetComponentInChildren<PokeInteractable>();
    }
}
