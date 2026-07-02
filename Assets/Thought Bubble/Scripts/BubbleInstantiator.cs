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
    }

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
        Destroy(currentDummyBubble); // destroy the dummy bubble if it exists
        currentDummyBubble = null; // clear any dummy bubble reference when spawning a real one
        GameObject bubbleObject = Instantiate(_bubblePrefab, origin.position, origin.rotation);
        // Track the live bubble so it's included in saves. It keeps the prefab's
        // default color; its state is snapshotted from the live components at save time.
        bubbleData.Register(bubbleObject.GetComponent<Bubble>());
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
    }

    public void DestroyDummyBubble()
    {
        if (currentDummyBubble != null)
        {
            Destroy(currentDummyBubble);
            currentDummyBubble = null;
        }
    }
}
