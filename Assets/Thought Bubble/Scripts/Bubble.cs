using UnityEngine;
using ThoughtBubble;
using Newtonsoft.Json;

/// <summary>
/// Lives on the bubble prefab. Owns the bubble's persistent state via the nested
/// <see cref="Data"/> DTO (the only thing serialized to JSON) and bridges it to
/// the live components (transform, BubbleColor, BubbleOrbitMotion).
/// </summary>
public class Bubble : MonoBehaviour
{
    /// <summary>Plain, Unity-free snapshot written to / read from JSON.</summary>
    [System.Serializable]
    public class Data
    {
        public double currentX, currentY, currentZ;
        public int r, g, b, a;            // 0–255 per channel
        public string transcription;
        public bool isMovingClockwise;
        public bool isOrbiting;

        public Data() { }                 // required for JSON deserialization

        // Convenience accessors for code only. [JsonIgnore] keeps Newtonsoft from
        // serializing these (a Vector3/Color getter recurses via 'normalized' and
        // throws a self-referencing loop). The persisted data is the fields above.
        [JsonIgnore] public Vector3 Position => new Vector3((float)currentX, (float)currentY, (float)currentZ);
        [JsonIgnore] public Color Color => new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }

    [SerializeField] private Data data = new Data();
    public Data BubbleData => data;

    private BubbleColor _color;
    private BubbleOrbitMotion _orbit;

    private void Awake()
    {
        _color = GetComponent<BubbleColor>();
        _orbit = GetComponent<BubbleOrbitMotion>();
    }

    /// <summary>Reads live world state into <see cref="data"/> and returns it (for saving).</summary>
    public Data CaptureSnapshot()
    {
        data.currentX = transform.position.x;
        data.currentY = transform.position.y;
        data.currentZ = transform.position.z;

        if (_color != null)
        {
            Color c = _color.Color;
            data.r = (int)(c.r * 255); data.g = (int)(c.g * 255);
            data.b = (int)(c.b * 255); data.a = (int)(c.a * 255);
        }
        if (_orbit != null)
        {
            data.isMovingClockwise = _orbit.clockwise;
            data.isOrbiting = _orbit.IsOrbiting;
        }
        return data;
    }

    /// <summary>Pushes loaded data onto this live bubble (call right after Instantiate).</summary>
    public void ApplyData(Data loaded)
    {
        data = loaded;
        transform.position = loaded.Position;
        if (_color != null) _color.Color = loaded.Color;
        if (loaded.isOrbiting && _orbit != null)
            _orbit.ResumeOrbit(loaded.isMovingClockwise);
    }
}
