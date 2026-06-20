using Oculus.Interaction;
using UnityEngine;

public class BubbleGrabBehavior : MonoBehaviour
{
    public ScenePropReference scenePropReference;
    private PointableUnityEventWrapper grabEventManager;

    void Awake()
    {
        scenePropReference = ScenePropReference.Instance;
        grabEventManager = GetComponent<PointableUnityEventWrapper>();
        if (grabEventManager != null)
        {
            grabEventManager.WhenSelect.AddListener(_ => GrabBubble());
            grabEventManager.WhenUnselect.AddListener(_ => ReleaseBubble());
        }
    }

    void Start()
    {
        if (scenePropReference != null)
        {
            // Do something with the referenced bubble menu
            if (scenePropReference.bubbleMenu != null)
            {
                // Do something with the referenced bubble menu
            }
        }
    }

    public void GrabBubble()
    {
        if (scenePropReference != null && scenePropReference.bubbleMenu != null)
        {
            scenePropReference.bubbleMenu.SetActive(true);
        }
    }

    public void ReleaseBubble()
    {
        if (scenePropReference != null && scenePropReference.bubbleMenu != null)
        {
            scenePropReference.bubbleMenu.SetActive(false);
        }
    }
}
