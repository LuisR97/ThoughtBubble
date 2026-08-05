using Oculus.Interaction;
using UnityEngine;

public class BubbleInstantiator : MonoBehaviour
{
    [Tooltip("The bubble prefab to spawn.")]
    [SerializeField] private GameObject _bubblePrefab;
    [SerializeField] private GameObject dummyBubblePrefab;
    public GameObject currentDummyBubble;
    [Tooltip("Where new bubbles appear. Leave empty to spawn at this object's position.")]
    [SerializeField] private Transform _spawnPoint;
    private PointableUnityEventWrapper buttonEventWrapper;
    private SavedBubbleData bubbleData;
    private MicrophoneRecorder microphoneRecorder;
    
    void Awake()
    {
        buttonEventWrapper = GetComponent<PointableUnityEventWrapper>();
        if (buttonEventWrapper == null)
        {
            Debug.LogError($"{nameof(BubbleInstantiator)}: no PointableUnityEventWrapper component found.", this);
            return;
        }
        buttonEventWrapper.WhenSelect.AddListener(ButtonPress);
    }

    void Start()
    {
        bubbleData = ScenePropReference.Instance.savedBubbles;
        microphoneRecorder = ScenePropReference.Instance.microphoneRecorder;
    }

    //Gets called from CONFIRM BUBBLE button in the Create Bubble menu 
    public void ButtonPress(PointerEvent evt)
    {
        Debug.Log("Button pressed.");
        SpawnBubble();
    }

    /// <summary>
    /// Instantiates a single instance of the bubble prefab at the spawn point
    /// (or this object's transform if no spawn point is set) and returns it.
    /// </summary>
    public GameObject SpawnBubble()
    {
        if (_bubblePrefab == null)
        {
            Debug.LogError($"{nameof(BubbleInstantiator)}: no bubble prefab assigned.", this);
            return null;
        }

        Transform origin = _spawnPoint != null ? _spawnPoint : transform;
        Destroy(currentDummyBubble);
        currentDummyBubble = null; 

        GameObject bubbleObject = Instantiate(_bubblePrefab, origin.position, origin.rotation);
        Bubble bubble = bubbleObject.GetComponent<Bubble>();
        // Finalize any in-progress recording and close the mic before saving it.
        if (microphoneRecorder != null) microphoneRecorder.CoolMic();
        bubble.BubbleData.audioFilePath = microphoneRecorder.SaveLastRecordingToFile();
        // Track the live bubble so it's included in saves. It keeps the prefab's
        // default color; its state is snapshotted from the live components at save time.
        bubbleData.Register(bubble);
        return bubbleObject;
    }

    public void SpawnDummyBubble(PointerEvent evt)
    {
        if (dummyBubblePrefab == null)
        {
            Debug.LogError($"{nameof(BubbleInstantiator)}: no dummy bubble prefab assigned.", this);
            return;
        }

        Transform origin = _spawnPoint != null ? _spawnPoint : transform;
        GameObject bubbleObject = Instantiate(dummyBubblePrefab, origin.position, origin.rotation);
        currentDummyBubble = bubbleObject;

        if (microphoneRecorder != null)
        {
            // Fresh create flow: drop any leftover recording so confirming without
            // recording produces no audio, not the previous bubble's.
            microphoneRecorder.DiscardLastRecording();

            // Desktop/Link has a ~1s WASAPI device-open freeze, so warm the mic now
            // (at Create Bubble) to hide it. Quest opens the mic in ~27ms with no
            // freeze, so there it just starts the device at record-press instead —
            // keeping the mic closed until the user actually records.
#if !UNITY_ANDROID
            microphoneRecorder.WarmMic();
#endif
        }
    }

    public void DestroyDummyBubble()
    {
        if (currentDummyBubble != null)
        {
            Destroy(currentDummyBubble);
            currentDummyBubble = null;
        }

        // Cancelled the create flow — close the mic (any recording is discarded).
        if (microphoneRecorder != null) microphoneRecorder.CoolMic();
    }
}
