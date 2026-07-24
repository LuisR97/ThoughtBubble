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
        openBubbleCreationMenuButton.Enable();
    }

}
