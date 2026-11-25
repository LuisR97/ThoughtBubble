using Oculus.Interaction.Surfaces;
using UnityEngine;

/// <summary>
/// The pokeable area of a Poke Wedge Button: a flat plane cut down to the wedge shape
/// described by <see cref="PokeWedge"/>. Sits on Model/Surface next to the PlaneSurface,
/// and is what the root's PokeInteractable points at as its Surface Patch.
///
/// Replaces the ClippedPlaneSurface + BoundsClipper pair from Poke Button Prefab. Those
/// can only trim the plane to an axis-aligned rectangle, which would give every wedge a
/// box-shaped hit area overlapping its neighbours. PokeInteractable only needs an
/// ISurfacePatch, and only asks it two questions: "what's the closest point on you?" and
/// "does this ray hit you?". Answering those for a ring segment is all it takes to get
/// wedge-shaped poking. The rest (press depth, hover distances, picking between two
/// neighbours) is the SDK's own logic, unchanged.
/// </summary>
public class WedgeSurface : MonoBehaviour, ISurfacePatch
{
    [Tooltip("The unbounded plane the wedge is cut from: the PlaneSurface on this object.")]
    [SerializeField] private PlaneSurface _planeSurface;

    [Tooltip("The PokeWedge on the button root, which describes the wedge's shape.")]
    [SerializeField] private PokeWedge _wedge;

    /// <summary>The unbounded plane this wedge is cut from. Press depth is measured against it.</summary>
    public ISurface BackingSurface => _planeSurface;

    /// <summary>The plane's transform. The poke interactor tracks surface movement through it.</summary>
    public Transform Transform => _planeSurface.Transform;

    private void Awake()
    {
        if (_planeSurface == null)
            Debug.LogError($"{nameof(WedgeSurface)} on '{name}': Plane Surface is not assigned.", this);
        if (_wedge == null)
            Debug.LogError($"{nameof(WedgeSurface)} on '{name}': Wedge is not assigned.", this);
    }

    /// <summary>
    /// The closest point on the wedge to <paramref name="point"/>, in world space. A point
    /// beside the wedge is pulled onto the wedge's nearest edge, the same way
    /// ClippedPlaneSurface pulls points onto its box. The SDK measures how far the fingertip
    /// is from the button sideways using this point, so it decides which wedge the finger
    /// is over.
    /// </summary>
    public bool ClosestSurfacePoint(in Vector3 point, out SurfaceHit hit, float maxDistance = 0)
    {
        if (!_planeSurface.ClosestSurfacePoint(point, out hit, maxDistance))
            return false;

        // The wedge shape is described in the button root's local space, so do the
        // clamping there and convert back.
        Transform space = _wedge.transform;
        Vector3 local = space.InverseTransformPoint(hit.Point);
        Vector2 clamped = _wedge.ClosestPoint(new Vector2(local.x, local.y));
        hit.Point = space.TransformPoint(new Vector3(clamped.x, clamped.y, local.z));
        hit.Distance = Vector3.Distance(point, hit.Point);
        return maxDistance <= 0 || hit.Distance <= maxDistance;
    }

    /// <summary>Raycast against the plane, counting only hits that land inside the wedge.</summary>
    public bool Raycast(in Ray ray, out SurfaceHit hit, float maxDistance = 0)
    {
        if (!_planeSurface.Raycast(ray, out hit, maxDistance))
            return false;

        Vector3 local = _wedge.transform.InverseTransformPoint(hit.Point);
        return _wedge.Contains(new Vector2(local.x, local.y));
    }
}
