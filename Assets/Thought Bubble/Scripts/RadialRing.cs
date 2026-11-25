using UnityEngine;

/// <summary>
/// One ring of a radial menu. Lays out the <see cref="PokeWedge"/> buttons that are its
/// direct children: they go clockwise in Hierarchy order, each one starting where the
/// previous one ended. Widening one wedge (its Arc Degrees) pushes every later wedge
/// round, and dragging wedges up or down the Hierarchy reorders them round the ring.
///
/// Stack several rings with different radii on the same centre to build a wheel. The
/// emotion wheel is three rings: cores, feelings, words.
///
/// The ring owns its wedges' inner/outer radius, gap and start angle, and snaps their
/// transforms to its own centre. A wedge's own Arc Degrees is the only thing set per
/// wedge. Radii are in the wedges' local units (1 unit = one standard poke button's
/// width; see <see cref="PokeWedge"/>), NOT this object's units. Under the menu
/// Canvases this object is in canvas units, which are 100x smaller.
/// </summary>
[ExecuteAlways]
public class RadialRing : MonoBehaviour
{
    [Tooltip("Distance from the centre to the inside edge of every wedge in this ring, in button units.")]
    [SerializeField] private float _innerRadius = 1f;

    [Tooltip("Distance from the centre to the outside edge of every wedge in this ring, in button units.")]
    [SerializeField] private float _outerRadius = 2f;

    [Tooltip("Where the first wedge begins, in degrees clockwise from 12 o'clock as seen by the user.")]
    [SerializeField] private float _startAngle = 0f;

    [Tooltip("Visible gap between neighbouring wedges, in button units. Drawing only: the pokeable areas still meet edge to edge.")]
    [SerializeField] private float _gap = 0.04f;

    private bool _layoutDirty = true;
    private float _lastWarnedTotal = -1f;

    /// <summary>Degrees used by all this ring's wedges together. Over 360 means wedges overlap.</summary>
    public float TotalDegrees { get; private set; }

    /// <summary>Ask for a re-layout on the next Update. Wedges call this when their arc changes.</summary>
    public void MarkLayoutDirty() => _layoutDirty = true;

    private void OnEnable()
    {
        Layout();
    }

    private void OnValidate()
    {
        _innerRadius = Mathf.Max(0f, _innerRadius);
        _outerRadius = Mathf.Max(_innerRadius + 0.01f, _outerRadius);
        _gap = Mathf.Max(0f, _gap);
        _layoutDirty = true;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    private void OnTransformChildrenChanged()
    {
        _layoutDirty = true;
    }

    private void Update()
    {
        // In the editor, lay out on every editor update. Dragging a wedge to a new place in
        // the Hierarchy should reorder the ring straight away, and Layout() only writes
        // values that actually changed, so this is cheap. At runtime, only when asked.
        if (_layoutDirty || !Application.isPlaying)
            Layout();
    }

    private void Layout()
    {
        _layoutDirty = false;
        float angle = _startAngle;

        foreach (Transform child in transform)
        {
            if (!child.TryGetComponent(out PokeWedge wedge))
                continue;

            // Every wedge shares the ring's centre. The wedges' shapes, not their
            // transforms, put them at different places round the ring.
            if (child.localPosition != Vector3.zero || child.localRotation != Quaternion.identity)
            {
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(child);
#endif
            }

            wedge.ApplyRingLayout(angle, _innerRadius, _outerRadius, _gap);
            angle += wedge.ArcDegrees;
        }

        TotalDegrees = angle - _startAngle;

        if (TotalDegrees > 360.01f && !Mathf.Approximately(TotalDegrees, _lastWarnedTotal))
        {
            _lastWarnedTotal = TotalDegrees;
            Debug.LogWarning($"[RadialRing] '{name}': wedges add up to {TotalDegrees:0.##} degrees, " +
                             "more than a full circle, so the last ones overlap the first.", this);
        }
    }
}
