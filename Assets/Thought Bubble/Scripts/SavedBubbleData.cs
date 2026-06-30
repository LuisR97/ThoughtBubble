using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

public class SavedBubbleData : MonoBehaviour
{
    [Tooltip("Bubbles loaded from / about to be saved to disk.")]
    public List<Bubble> bubbles = new List<Bubble>();
    public List<GameObject> bubbleObjects = new List<GameObject>();
    // persistentDataPath is the one writable, persistent location on both PC and Quest.
    private string SavePath => Path.Combine(Application.persistentDataPath, "bubbles.json");

    void Start()
    {
        Load();
        SpawnBubblePrefabsFromList();
    }

    /// <summary>Reads the saved JSON into the list. Called automatically at startup. Loads Bubble data but not the actual GameObjects.</summary>
    public void Load()
    {
        if (!File.Exists(SavePath))
        {
            bubbles = new List<Bubble>(); 
            Debug.Log("No saved bubble data found; starting with an empty list.");
            return;
        }

        string json = File.ReadAllText(SavePath);
        bubbles = JsonConvert.DeserializeObject<List<Bubble>>(json) ?? new List<Bubble>();
        Debug.Log($"Loaded {bubbles.Count} bubbles from {SavePath}");
    }
    
    /// <summary>Spawns GameObjects for each Bubble in the list.</summary>
    public void SpawnBubblePrefabsFromList()
    {
        foreach (Bubble bubble in bubbles)
        {
            bubbleObjects.Add(Instantiate(ScenePropReference.Instance.bubblePrefab, new Vector3((float)bubble.currentX, (float)bubble.currentY, (float)bubble.currentZ), Quaternion.identity));
        }
    }

    /// <summary>Writes the current list to disk as JSON.</summary>
    public void SaveToFile()
    {
        // Update the list of bubbles with the current state of the bubble objects
        bubbles = SaveCurrentList();
        string json = JsonConvert.SerializeObject(bubbles, Formatting.Indented);
        File.WriteAllText(SavePath, json);
        Debug.Log($"Saved {bubbles.Count} bubbles to {SavePath}");
    }

    public List<Bubble> SaveCurrentList()
    {
        List<Bubble> currentBubbles = new List<Bubble>(bubbles);
        for(int i = 0; i < bubbleObjects.Count; i++)
        {
            currentBubbles[i] = new Bubble(
                bubbleObjects[i].transform.position.x,
                bubbleObjects[i].transform.position.y,
                bubbleObjects[i].transform.position.z,
                bubbles[i].r,
                bubbles[i].g,
                bubbles[i].b,
                bubbles[i].a,
                bubbles[i].transcription,
                bubbles[i].isMovingClockwise
            );
        }
        return currentBubbles;
    }

    /// <summary>Adds a bubble to the in-memory list (call Save() to persist it).</summary>
    public void AddBubble(Vector3 position, Color color, string transcription, bool isMovingClockwise) 
    {
        //TO DO: Add real transcription and audio recordings
        bubbles.Add(new Bubble(
            position.x, position.y, position.z,
            (int)(color.r * 255), (int)(color.g * 255), (int)(color.b * 255), (int)(color.a * 255),
            "",
            false
        ));
    }

    //TO-DO
    public void RemoveBubble(Bubble bubble)
    {

    }


    /// <summary>
    /// The reliable "session ended" signal on Quest/Android: the OS suspends the
    /// app (headset removed, Meta button, app switch) instead of quitting it, so
    /// OnApplicationQuit often never runs but this always does.
    /// </summary>
    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
            SaveToFile();
    }

    /// <summary>Clean exit on desktop / Editor stop.</summary>
    private void OnApplicationQuit()
    {
        SaveToFile();
    }

}
