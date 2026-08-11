using Oculus.Interaction;
using UnityEngine;
using TMPro;
using UnityEngine.Events;

public class MainMenu : MonoBehaviour
{
    private ScenePropReference scenePropReference;
    public GameObject mainScreen, bubbleCreationMenu;
    public PokeInteractableToggle openBubbleCreationMenuButton;

    void Start()
    {
        scenePropReference = ScenePropReference.Instance;
    }

    void OnEnable()
    {
        //bubbleCreationMenu.SetActive(false);
        //mainScreen.SetActive(true);
        
        // Force a real State transition so InteractableColorVisual restarts its colour
        // fade. Enable() alone is a no-op here (the interactable is already enabled), and
        // the visual caches its target, so without a state change it keeps whatever partial
        // colour was left when PreInitializeHidden killed the fade mid-lerp at start-up.
        openBubbleCreationMenuButton.Disable();
        openBubbleCreationMenuButton.Enable();
    }

}
