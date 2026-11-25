using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Turns a poke button into a ring segment ("wedge") instead of a rectangle. Lives on
/// the root of a Poke Wedge Button Prefab instance. It holds the wedge's shape, builds
/// the mesh the button draws, and centres the label on the wedge. <see cref="WedgeSurface"/>
/// reads the same shape, so the area you can poke is the area you can see.
///
/// The button root sits at the CENTRE of the wheel, not in the middle of the wedge.
/// Every wedge in a wheel shares one origin, and the shape alone says where the wedge
/// is: two radii and two angles. That keeps the press animation simple, because
/// PokeInteractableVisual only slides ButtonVisual straight back along the root's
/// forward axis, whatever shape it is drawing.
///
/// Units are the button root's local units, the same units the rest of Poke Button
/// Prefab is built in: 1 unit is one standard button's width (5 cm under the current
/// menu Canvases: Canvas scale 0.0005 x button root scale 100).
///
/// Angles are in degrees clockwise from 12 o'clock, as seen by the user poking the front.
///
/// If the wedge is a direct child of a <see cref="RadialRing"/>, the ring owns the radii,
/// the gap and the start angle and overwrites them. Only <see cref="ArcDegrees"/> belongs
/// to the wedge.
/// </summary>
[ExecuteAlways]
public class PokeWedge : MonoBehaviour
{
    [Tooltip("Distance from the wheel centre to the inside edge of the wedge, in button units. " +
             "Set by the parent RadialRing if there is one.")]
    [SerializeField] private float _innerRadius = 1f;

    [Tooltip("Distance from the wheel centre to the outside edge of the wedge, in button units. " +
             "Set by the parent RadialRing if there is one.")]
    [SerializeField] private float _outerRadius = 2f;

    [Tooltip("Where the wedge begins, in degrees clockwise from 12 o'clock. " +
             "Set by the parent RadialRing if there is one.")]
    [SerializeField] private float _startAngle = 0f;

    [Tooltip("How wide this wedge is, in degrees. Inside a RadialRing, changing it pushes " +
             "every later wedge round the ring.")]
    [SerializeField, Range(0.1f, 360f)] private float _arcDegrees = 30f;

    [Tooltip("Visible gap between neighbouring wedges, in button units. Drawing only: the " +
             "pokeable areas still meet edge to edge, so there is no dead zone between buttons. " +
             "Set by the parent RadialRing if there is one.")]
    [SerializeField] private float _gap = 0.04f;

    [Header("Wiring")]
    [Tooltip("The MeshFilter on Visuals/ButtonVisual. This script generates its mesh.")]
    [SerializeField] private MeshFilter _meshFilter;

    [Tooltip("The button label (BUTTON LABEL TMP_Text). Moved to the middle of the wedge and " +
             "turned to read along the radius, like the printed emotion wheel.")]
    [SerializeField] private RectTransform _label;

    // Mesh resolution along the arc. At the 6-unit outer radius of the emotion wheel,
    // a 2.5-degree step bows less than 0.1 mm from a true circle.
    private const float DegreesPerSegment = 2.5f;

    // Fraction of the wedge the label's box may cover, so text never touches the edges.
    private const float LabelFill = 0.85f;

    private Mesh _mesh;
    private bool _dirty = true;

    /// <summary>Inside edge of the wedge, in button units from the wheel centre.</summary>
    public float InnerRadius => _innerRadius;

    /// <summary>Outside edge of the wedge, in button units from the wheel centre.</summary>
    public float OuterRadius => _outerRadius;

    /// <summary>Where the wedge begins, in degrees clockwise from 12 o'clock.</summary>
    public float StartAngle => _startAngle;

    /// <summary>How wide the wedge is, in degrees.</summary>
    public float ArcDegrees => _arcDegrees;

    /// <summary>The angle through the middle of the wedge.</summary>
    public float MidAngle => _startAngle + _arcDegrees * 0.5f;

    // The button's press events (When Select, When Release, ...). It sits on this same
    // root object, so it's found here rather than wired by hand on every wedge.
    private PointableUnityEventWrapper _pokeButton;

    private void Awake()
    {
        _pokeButton = GetComponent<PointableUnityEventWrapper>();
    }

    private void OnEnable()
    {
        Rebuild();
        _pokeButton.WhenSelect.AddListener(PrintLabel);
    }

    // The mesh is regenerated on every enable, so free it on every disable. This also
    // covers script recompiles: Unity disables everything before a recompile and wipes
    // the _mesh field, so without this each recompile would orphan one mesh per wedge.
    private void OnDisable()
    {
        if (_mesh == null)
            return;

        if (Application.isPlaying)
            Destroy(_mesh);
        else
            DestroyImmediate(_mesh);
        _mesh = null;

        _pokeButton.WhenSelect.RemoveListener(PrintLabel);
    }

    // Only flag the rebuild here. Assigning a mesh or moving a UI RectTransform inside
    // OnValidate makes Unity warn "SendMessage cannot be called during ... OnValidate".
    private void OnValidate()
    {
        _innerRadius = Mathf.Max(0f, _innerRadius);
        _outerRadius = Mathf.Max(_innerRadius + 0.01f, _outerRadius);
        _gap = Mathf.Max(0f, _gap);
        _dirty = true;

        if (transform.parent != null && transform.parent.TryGetComponent(out RadialRing ring))
            ring.MarkLayoutDirty();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    private void Update()
    {
        if (_dirty)
            Rebuild();
    }

    /// <summary>
    /// Called by the parent <see cref="RadialRing"/> to place this wedge. Only writes, and
    /// only marks the scene changed, when a value actually differs.
    /// </summary>
    public void ApplyRingLayout(float startAngle, float innerRadius, float outerRadius, float gap)
    {
        if (Mathf.Approximately(_startAngle, startAngle) &&
            Mathf.Approximately(_innerRadius, innerRadius) &&
            Mathf.Approximately(_outerRadius, outerRadius) &&
            Mathf.Approximately(_gap, gap))
            return;

        _startAngle = startAngle;
        _innerRadius = innerRadius;
        _outerRadius = outerRadius;
        _gap = gap;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }
#endif

        // Rebuild now rather than waiting for this wedge's own Update, which may already
        // have run this editor tick. But not while inactive: OnDisable/OnDestroy never run
        // for an object that is never enabled, so a mesh made now would never be freed.
        // OnEnable rebuilds anyway.
        if (isActiveAndEnabled)
            Rebuild();
        else
            _dirty = true;
    }

    /// <summary>
    /// The point <paramref name="radius"/> from the wheel centre at <paramref name="angle"/>
    /// degrees clockwise from 12 o'clock, in the button root's local XY plane.
    /// </summary>
    public static Vector2 PolarToLocal(float angle, float radius)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * radius;
    }

    /// <summary>
    /// True if a point in the button root's local XY plane is inside the wedge. The visual
    /// gap is ignored, so neighbouring wedges' pokeable areas meet with no dead zone.
    /// </summary>
    public bool Contains(Vector2 point)
    {
        float radius = point.magnitude;
        return radius >= _innerRadius && radius <= _outerRadius && AngleFromStart(point) <= _arcDegrees;
    }

    /// <summary>
    /// The point inside the wedge closest to <paramref name="point"/>, both in the button
    /// root's local XY plane. Gap ignored, as in <see cref="Contains"/>.
    /// </summary>
    public Vector2 ClosestPoint(Vector2 point)
    {
        float radius = point.magnitude;
        if (radius < 1e-6f)
            return PolarToLocal(MidAngle, _innerRadius);

        // Within the wedge's angle range: only the distance from the centre can be wrong.
        if (AngleFromStart(point) <= _arcDegrees)
            return point / radius * Mathf.Clamp(radius, _innerRadius, _outerRadius);

        // Beside the wedge: the closest point is on one of its two straight edges.
        Vector2 onStartEdge = ClosestOnEdge(point, _startAngle);
        Vector2 onEndEdge = ClosestOnEdge(point, _startAngle + _arcDegrees);
        return (point - onStartEdge).sqrMagnitude <= (point - onEndEdge).sqrMagnitude
            ? onStartEdge
            : onEndEdge;
    }

    private Vector2 ClosestOnEdge(Vector2 point, float edgeAngle)
    {
        Vector2 direction = PolarToLocal(edgeAngle, 1f);
        return direction * Mathf.Clamp(Vector2.Dot(point, direction), _innerRadius, _outerRadius);
    }

    // How far clockwise from the wedge's start edge a point lies, from 0 up to 360.
    private float AngleFromStart(Vector2 point)
    {
        float angle = Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg;
        return Mathf.Repeat(angle - _startAngle, 360f);
    }

    private void Rebuild()
    {
        _dirty = false;
        BuildMesh();
        PlaceLabel();
    }

    // The mesh is built in ButtonVisual's local space using root-local coordinates.
    // That is valid because ButtonVisual has no rotation or scale relative to the root.
    // It only sits 0.3 units in front, which is where the drawn button should be anyway.
    private void BuildMesh()
    {
        if (_meshFilter == null)
            return;

        if (_mesh == null)
            _mesh = new Mesh { name = "Poke Wedge (generated)", hideFlags = HideFlags.DontSave };

        int segments = Mathf.Max(1, Mathf.CeilToInt(_arcDegrees / DegreesPerSegment));

        // Trim both side edges by half the gap. Turning a fixed WIDTH into an angle depends
        // on the radius (asin(halfGap / r)), so the inner and outer edges are trimmed by
        // different angles. That keeps the gap the same width all the way out, instead of
        // a sliver at the centre widening into a wide slot at the rim.
        TrimmedEdge(_innerRadius, out float innerFrom, out float innerTo);
        TrimmedEdge(_outerRadius, out float outerFrom, out float outerTo);

        var vertices = new Vector3[(segments + 1) * 2];
        var uvs = new Vector2[vertices.Length];
        var normals = new Vector3[vertices.Length];
        var triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            vertices[i * 2] = PolarToLocal(Mathf.Lerp(innerFrom, innerTo, t), _innerRadius);
            vertices[i * 2 + 1] = PolarToLocal(Mathf.Lerp(outerFrom, outerTo, t), _outerRadius);

            // U runs along the arc and V from the inner edge to the outer edge. The button's
            // RoundedPanelMesh shader draws its rectangle in UV space, not from the mesh
            // outline, so with these UVs it fills the wedge edge to edge, the way it fills a
            // quad. The prefab's colours and hover/press fades then work unchanged.
            uvs[i * 2] = new Vector2(t, 0f);
            uvs[i * 2 + 1] = new Vector2(t, 1f);

            // The user pokes from -Z (ButtonVisual sits on that side of the surface).
            normals[i * 2] = Vector3.back;
            normals[i * 2 + 1] = Vector3.back;
        }

        for (int i = 0; i < segments; i++)
        {
            int inner0 = i * 2, outer0 = inner0 + 1, inner1 = inner0 + 2, outer1 = inner0 + 3;

            // Clockwise as seen from -Z, so the faces point at the user.
            triangles[i * 6 + 0] = inner0;
            triangles[i * 6 + 1] = outer0;
            triangles[i * 6 + 2] = outer1;
            triangles[i * 6 + 3] = inner0;
            triangles[i * 6 + 4] = outer1;
            triangles[i * 6 + 5] = inner1;
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.uv = uvs;
        _mesh.normals = normals;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();

        if (_meshFilter.sharedMesh != _mesh)
            _meshFilter.sharedMesh = _mesh;
    }

    private void TrimmedEdge(float radius, out float fromAngle, out float toAngle)
    {
        float halfGap = _gap * 0.5f;
        float trim = radius > halfGap ? Mathf.Asin(halfGap / radius) * Mathf.Rad2Deg : 90f;
        fromAngle = _startAngle + trim;
        toAngle = _startAngle + _arcDegrees - trim;

        // At this radius the gap is wider than the wedge: collapse the edge to a point.
        if (fromAngle > toAngle)
            fromAngle = toAngle = MidAngle;
    }

    private void PlaceLabel()
    {
        if (_label == null)
            return;

        float midRadius = (_innerRadius + _outerRadius) * 0.5f;
        Vector2 centre = PolarToLocal(MidAngle, midRadius);

        // 90 - angle turns the text's baseline to point outward along the radius. Folding
        // the result into [-90, 90) keeps every label right way up: on the left half of
        // the wheel the text reads from the rim inward instead, as on the printed wheel.
        float spin = Mathf.Repeat(90f - MidAngle + 90f, 180f) - 90f;

        // The text runs along the radius, so the box is "depth of the ring" long and
        // "width of the wedge at its middle" tall.
        float depth = _outerRadius - _innerRadius;
        float across = 2f * midRadius * Mathf.Sin(Mathf.Min(_arcDegrees, 180f) * 0.5f * Mathf.Deg2Rad) - _gap;
        Vector3 labelScale = _label.localScale;
        Vector2 size = new Vector2(
            depth * LabelFill / Mathf.Max(labelScale.x, 1e-6f),
            Mathf.Max(0f, across) * LabelFill / Mathf.Max(labelScale.y, 1e-6f));

        // Keep the label's own Z (its small offset in front of the plate).
        Vector3 position = new Vector3(centre.x, centre.y, _label.localPosition.z);
        Quaternion rotation = Quaternion.Euler(0f, 0f, spin);

        // Only write on change, so opening the scene doesn't mark 100+ labels modified.
        if (_label.localPosition != position)
            _label.localPosition = position;
        if (_label.localRotation != rotation)
            _label.localRotation = rotation;
        if (_label.sizeDelta != size)
            _label.sizeDelta = size;
    }

    private void PrintLabel(PointerEvent pointerEvent)
    {
        string labelString = _label.gameObject.GetComponent<TMPro.TextMeshProUGUI>().text;
        ScenePropReference.Instance.debugOnScreenText2.text = "Last Poke Button Pressed: " + labelString;
        Debug.Log("Last Poke Button Pressed: " + labelString);
    }
}
