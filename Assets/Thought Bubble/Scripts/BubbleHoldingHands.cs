using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

/// <summary>
/// Lives on the bubble prefab root. Keeps track of which hand(s) are currently
/// holding this bubble, so other scripts can ask e.g.
/// <c>IsHeldBy(Handedness.Left)</c>.
///
/// It listens to the bubble's own grab interactables instead of reading the
/// PointerEvent that BubbleGrabBehavior receives, because the SDK documents
/// PointerEvent.Data as having no formal contract. The interactables'
/// "selecting interactor added / removed" events hand over the actual grabber.
///
/// Timing: on grab these events fire BEFORE BubbleGrabBehavior.OnSelect, so
/// OnSelect can already query this. On release they also fire first, so by the
/// time OnUnselect runs the releasing hand has already been cleared.
/// </summary>
public class BubbleHoldingHands : MonoBehaviour
{
    // Indexed by Handedness (Left = 0, Right = 1). A count rather than a bool
    // because one hand owns several grab interactors (near, distance, controller),
    // and both hands may be holding the bubble at once.
    private readonly int[] _holdCounts = new int[2];

    private readonly List<IInteractableView> _grabInteractables = new List<IInteractableView>();

    /// <summary>True while the given hand is holding this bubble.</summary>
    public bool IsHeldBy(Handedness hand) => _holdCounts[(int)hand] > 0;

    private void Awake()
    {
        // Only the four grab types are gathered, so a poke or ray interactable
        // added to the bubble later would not count as "holding" it.
        _grabInteractables.AddRange(GetComponentsInChildren<HandGrabInteractable>(true));
        _grabInteractables.AddRange(GetComponentsInChildren<DistanceHandGrabInteractable>(true));
        _grabInteractables.AddRange(GetComponentsInChildren<GrabInteractable>(true));
        _grabInteractables.AddRange(GetComponentsInChildren<DistanceGrabInteractable>(true));
    }

    private void OnEnable()
    {
        foreach (IInteractableView interactable in _grabInteractables)
        {
            interactable.WhenSelectingInteractorViewAdded += OnGrabberAdded;
            interactable.WhenSelectingInteractorViewRemoved += OnGrabberRemoved;
        }
    }

    private void OnDisable()
    {
        foreach (IInteractableView interactable in _grabInteractables)
        {
            interactable.WhenSelectingInteractorViewAdded -= OnGrabberAdded;
            interactable.WhenSelectingInteractorViewRemoved -= OnGrabberRemoved;
        }

        // Unity doesn't guarantee whether this runs before or after the
        // interactables' own OnDisable, so their release events may never reach
        // us. Start clean when re-enabled rather than carry a stale hold.
        _holdCounts[0] = 0;
        _holdCounts[1] = 0;
    }

    private void OnGrabberAdded(IInteractorView grabber)
    {
        if (!TryGetHandedness(grabber, out Handedness hand))
            return;

        _holdCounts[(int)hand]++;
        Debug.Log($"{name} grabbed by {hand} hand.");
    }

    private void OnGrabberRemoved(IInteractorView grabber)
    {
        if (!TryGetHandedness(grabber, out Handedness hand))
            return;

        // Clamped because a release can arrive for a grab that began before this
        // component was enabled, which was never counted.
        _holdCounts[(int)hand] = Mathf.Max(0, _holdCounts[(int)hand] - 1);
        Debug.Log($"{name} released by {hand} hand.");
    }

    private bool TryGetHandedness(IInteractorView grabber, out Handedness hand)
    {
        // Hand-based grabbers know their hand directly. This includes the rig's
        // "hands on controller" mode, which drives a synthetic hand.
        IHand grabberHand = grabber switch
        {
            HandGrabInteractor g => g.Hand,
            DistanceHandGrabInteractor g => g.Hand,
            _ => null
        };
        if (grabberHand != null)
        {
            hand = grabberHand.Handedness;
            return true;
        }

        // Plain controller grabbers (GrabInteractor, DistanceGrabInteractor) have
        // no hand. In Meta's rig prefabs each carries a ControllerRef on its own
        // GameObject, which GetComponentInParent also checks.
        IController controller = (grabber as Component)?.GetComponentInParent<IController>();
        if (controller != null)
        {
            hand = controller.Handedness;
            return true;
        }

        hand = default;
        Debug.LogWarning($"{nameof(BubbleHoldingHands)}: couldn't tell which hand '{grabber}' belongs to; ignoring it.", this);
        return false;
    }
}
