using UnityEngine;

public class ScenePropReference : MonoBehaviour
{
    //MAKE SURE THAT THIS OBJECT IS INTANTIATED BEFORE ANY OTHER SCRIPTS TRY TO ACCESS IT
    //This is a singleton, so only one instance should exist at a time
    public static ScenePropReference Instance {get; private set;}
    public GameObject bubbleMenu;

    void Awake()
    {
        Instance = this;
    }
}
