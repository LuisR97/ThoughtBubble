using UnityEngine;

public class BubbleMenu : MonoBehaviour
{
    public ScenePropReference scenePropReference;

    void Start()
    {
        scenePropReference = ScenePropReference.Instance;
        if (scenePropReference != null)
        {
            // Do something with the referenced bubble menu
            if (scenePropReference.bubbleMenu != null)
            {
                // Do something with the referenced bubble menu
            }
        }
    }

    
}
