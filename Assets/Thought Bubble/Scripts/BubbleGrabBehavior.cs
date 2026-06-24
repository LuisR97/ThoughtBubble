using System.Collections;
using Oculus.Interaction;
using UnityEngine;

public class BubbleGrabBehavior : MonoBehaviour
{
    public ScenePropReference scenePropReference;
    private PointableUnityEventWrapper grabEventManager; //This is the component that handles the grab events

    [Tooltip("How long the bubble must be held before the menu appears (seconds).")]
    [SerializeField] private float grabDelay = 0.5f;
    private Coroutine grabRoutine;

    void Awake()
    {
        grabEventManager = GetComponent<PointableUnityEventWrapper>();
        if (grabEventManager != null)
        {
            grabEventManager.WhenSelect.AddListener(OnSelect);
            grabEventManager.WhenUnselect.AddListener(OnUnselect);
        }
    }

    void Start()
    {
        scenePropReference = ScenePropReference.Instance;
    }


    // Called when the bubble is grabbed. Add any further grab-time logic here.
    private void OnSelect(PointerEvent evt)
    {
        grabRoutine = StartCoroutine(GrabBubble());
    }

    // Called when the bubble is released. Add any further release-time logic here.
    private void OnUnselect(PointerEvent evt)
    {
        // If the user let go before the delay elapsed, cancel the
        // pending grab so the menu never appears.
        if (grabRoutine != null)
        {
            StopCoroutine(grabRoutine);
            grabRoutine = null;
        }
        StartCoroutine(ReleaseBubble());
    }


    public IEnumerator GrabBubble()
    {
        // Wait out the hold delay. If the user releases first, the unselect
        // listener stops this coroutine before we ever reach the code below.
        yield return new WaitForSeconds(grabDelay);

        if (scenePropReference != null && scenePropReference.bubbleMenu != null)
        {
            scenePropReference.bubbleMenu.SetActive(true);
        }
        else
        {
            Debug.LogError("Failed to grab bubble: Missing scene prop reference or bubble menu.");
        }

        // Grab completed; nothing left to cancel.
        grabRoutine = null;
    }

    public IEnumerator ReleaseBubble()
    {
        if (scenePropReference != null && scenePropReference.bubbleMenu != null)
        {
            scenePropReference.bubbleMenu.SetActive(false);
        }
        else
        {
            Debug.LogError("Failed to release bubble: Missing scene prop reference or bubble menu.");
        }

        yield break;
    }
}
