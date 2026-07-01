using Oculus.Interaction;
using UnityEngine;

public class ScenePropReference : MonoBehaviour
{
    //MAKE SURE THAT THIS OBJECT IS INTANTIATED BEFORE ANY OTHER SCRIPTS TRY TO ACCESS IT
    //YOU'LL HAVE TO REFERENCE THIS OBJECT IN START() FOR OTHER SCRIPTS TO ACCESS IT
    //This is a singleton, so only one instance should exist at a time
    public static ScenePropReference Instance {get; private set;}
    public GameObject bubbleMenu;
    public GameObject mainMenu;
    public GameObject bubbleInstantiatorMenu;
    public PointableUnityEventWrapper leftWristButton;
    public PointableUnityEventWrapper rightWristButton;
    public SavedBubbleData savedBubbles;
    public GameObject bubblePrefab;

    [Tooltip("Optional: the user's head (e.g. CenterEyeAnchor). If unset, uses Camera.main.")]
    public Transform headForOrbitCenter;
    [Tooltip("Captured once at startup: the floor point under the user's start position. " +
             "Thrown bubbles orbit around this fixed point.")]
    public Vector3 orbitCenter;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Capture where the user initially stands, projected to the floor (y = 0),
        // so orbiting bubbles revolve around that fixed point (they do not follow the user).
        Transform head = headForOrbitCenter != null
            ? headForOrbitCenter
            : (Camera.main != null ? Camera.main.transform : null);
        Vector3 p = head != null ? head.position : Vector3.zero;
        orbitCenter = new Vector3(p.x, 0f, p.z);
    }
}
