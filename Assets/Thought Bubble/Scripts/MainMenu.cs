using System.Collections;
using Oculus.Interaction;
using UnityEngine;
using TMPro;
using UnityEngine.Events;

public class MainMenu : MonoBehaviour
{
    private ScenePropReference scenePropReference;
    public GameObject mainScreen, bubbleCreationMenu;
    public PokeInteractableToggle openBubbleCreationMenuButton, otherRandomButton;

    void Start()
    {
        scenePropReference = ScenePropReference.Instance;
    }

    void OnEnable()
    {
        //bubbleCreationMenu.SetActive(false);
        //mainScreen.SetActive(true);

        StartCoroutine(KickButtonColours());
    }

    // Force a real State transition so InteractableColorVisual restarts its colour fade.
    // Enable() alone is a no-op here (the interactable is already enabled), and the visual
    // caches its target, so without a state change it keeps whatever partial colour was
    // left when PreInitializeHidden killed the fade mid-lerp at start-up.
    //
    // The one-frame wait matters. This component sits on Main Screen, an ancestor of the
    // buttons, and Unity dispatches OnEnable parent-first. InteractableColorVisual
    // unsubscribes from WhenStateChanged in OnDisable and only re-subscribes in its own
    // OnEnable — which happens later in the same activation walk. Kicking the state
    // immediately fires both transitions into a void: the visual never hears them, and its
    // cached _target then makes the next UpdateVisual() a no-op, so the stale colour sticks.
    private IEnumerator KickButtonColours()
    {
        yield return null;

        openBubbleCreationMenuButton.Disable();
        openBubbleCreationMenuButton.Enable();
        otherRandomButton.Disable();
        otherRandomButton.Enable();
    }

}
