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
    public PointableUnityEventWrapper leftWristButton;
    public PointableUnityEventWrapper rightWristButton;

    void Awake()
    {
        Instance = this;
    }
}
