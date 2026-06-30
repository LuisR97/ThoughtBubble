using Oculus.Interaction;
using UnityEngine;

public class BubbleInstantiator : MonoBehaviour
{
    [Tooltip("The bubble prefab to spawn.")]
    [SerializeField] private GameObject _bubblePrefab;

    [Tooltip("Where new bubbles appear. Leave empty to spawn at this object's position.")]
    [SerializeField] private Transform _spawnPoint;
    public PointableUnityEventWrapper buttonEventWrapper;
    public SavedBubbleData bubbleData;

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
        //add bubble data to the bubbles list in SavedBubbleData
        bubbleData.AddBubble(
            new Vector3(origin.position.x, origin.position.y, origin.position.z),
            new Color(1, 1, 1, 1),
            "",
            false
        );
        GameObject bubbleObject = Instantiate(_bubblePrefab, origin.position, origin.rotation);
        //add the new bubble object to the bubbleObjects list in SavedBubbleData
        bubbleData.bubbleObjects.Add(bubbleObject);
        return bubbleObject;
    }
}
