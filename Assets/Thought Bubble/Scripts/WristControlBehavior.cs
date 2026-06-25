using Oculus.Interaction;
using UnityEngine;

public class WristControlBehavior : MonoBehaviour
{
    public ScenePropReference scenePropReference;
    public PointableUnityEventWrapper buttonEventWrapper;
    public bool isMainMenuVisible = false;
    public GameObject mainMenu;

    void Awake()
    {
        buttonEventWrapper = GetComponent<PointableUnityEventWrapper>();
        buttonEventWrapper.WhenSelect.AddListener(WristButtonPress);
    }

    void Start()
    {
        scenePropReference = ScenePropReference.Instance;
        mainMenu = scenePropReference.mainMenu;
    }

    public void WristButtonPress(PointerEvent evt)
    {
        if (!isMainMenuVisible)
        {
            ShowMainMenu();
            Debug.Log("Main menu shown.");
            isMainMenuVisible = true;
        }
        else
        {
            HideMainMenu();
            Debug.Log("Main menu hidden.");
            isMainMenuVisible = false;
        }
    }

    public void ShowMainMenu()
    {
        mainMenu.SetActive(true);
    }

    public void HideMainMenu()
    {
        mainMenu.SetActive(false);
    }

    
}
