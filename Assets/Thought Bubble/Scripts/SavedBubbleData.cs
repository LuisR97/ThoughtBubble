using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class SavedBubbleData : MonoBehaviour
{
    [Tooltip("Live bubbles in the scene — the single source of truth at runtime.")]
    public List<Bubble> bubbles = new List<Bubble>();

    // persistentDataPath is the one writable, persistent location on both PC and Quest.
    private string SavePath => Path.Combine(Application.persistentDataPath, "bubbles.json");

    void Start()
    {
        LoadAndSpawn();
    }

    /// <summary>Reads the saved JSON and spawns a live bubble for each entry.</summary>
    public void LoadAndSpawn()
    {
        foreach (Bubble.Data d in Load())
        {
            GameObject obj = Instantiate(ScenePropReference.Instance.bubblePrefab, d.Position, Quaternion.identity);
            Bubble bubble = obj.GetComponent<Bubble>();
            if (bubble == null)
            {
                Debug.LogError("Bubble prefab is missing its Bubble component.", obj);
                continue;
            }
            bubble.ApplyData(d);     // sets color + resumes orbit if it was orbiting
            bubbles.Add(bubble);
        }
        Debug.Log($"Spawned {bubbles.Count} bubbles.");
    }

    /// <summary>Reads the saved JSON into a list of plain data snapshots.</summary>
    private List<Bubble.Data> Load()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("No saved bubble data found; starting with an empty list.");
            return new List<Bubble.Data>();
        }

        string json = File.ReadAllText(SavePath);
        List<Bubble.Data> loaded = JsonConvert.DeserializeObject<List<Bubble.Data>>(json);
        Debug.Log($"Loaded {(loaded?.Count ?? 0)} bubbles from {SavePath}");
        return loaded ?? new List<Bubble.Data>();
    }

    /// <summary>Snapshots every live bubble and writes the list to disk as JSON.</summary>
    public void SaveToFile()
    {
        List<Bubble.Data> snapshot = new List<Bubble.Data>(bubbles.Count);
        foreach (Bubble b in bubbles)
            if (b != null)
                snapshot.Add(b.CaptureSnapshot());

        string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
        File.WriteAllText(SavePath, json);
        Debug.Log($"Saved {snapshot.Count} bubbles to {SavePath}");
    }

    /// <summary>Track a newly created bubble so it's included in saves.</summary>
    public void Register(Bubble bubble)
    {
        if (bubble != null && !bubbles.Contains(bubble))
            bubbles.Add(bubble);
    }

    /// <summary>Stop tracking a bubble (call before destroying it).</summary>
    public void Remove(Bubble bubble)
    {
        bubbles.Remove(bubble);
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
