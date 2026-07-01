using UnityEngine;

/// <summary>
/// Drives a thrown bubble into a fixed orbit around the user's starting position.
///
/// On release the bubble keeps its throw velocity (the prefab's Rigidbody has no
/// gravity) and flies straight. Once it has travelled <see cref="curveStartDistance"/>
/// it curves and eases into a horizontal circle. Each bubble keeps the Y it had
/// when it locked in, and its ring radius is derived from that height so all the
/// bubbles sit on the surface of a half-sphere (dome) of radius <see cref="domeRadius"/>:
///     ringRadius(h) = sqrt(domeRadius^2 - h^2)
/// Higher bubbles orbit tighter, eye-level bubbles orbit wide.
///
/// Put this on the bubble prefab (it has the required Rigidbody) and call
/// <see cref="OnReleased"/> when the bubble is thrown.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BubbleOrbitMotion : MonoBehaviour
{
    private enum Phase { Idle, Flying, Orbiting }

    [Header("Dome")]
    [Tooltip("Center to orbit. If null, uses ScenePropReference.Instance.orbitCenter " +
             "(captured at the user's start position).")]
    [SerializeField] private Transform domeCenter;
    [Tooltip("Radius of the half-sphere the bubbles settle onto (m).")]
    [SerializeField] private float domeRadius = 2.5f;
    [Tooltip("Smallest ring radius, so bubbles near the top of the dome don't collapse to a point.")]
    [SerializeField] private float minRingRadius = 0.4f;
    [Tooltip("Orbit travel speed along the ring (m/s).")]
    [SerializeField] private float orbitSpeed = 1.5f;

    [Header("Direction")]
    [Tooltip("Pick a random left/right each throw. Turn off to drive 'clockwise' yourself.")]
    [SerializeField] private bool randomizeDirection = true;
    [Tooltip("Clockwise (seen from above) when not randomizing.")]
    public bool clockwise = false;

    [Header("Throw -> curve")]
    [Tooltip("Min release speed to count as a throw; gentle releases are ignored.")]
    [SerializeField] private float throwSpeedThreshold = 0.5f;
    [Tooltip("How far it flies straight before curving (m).")]
    [SerializeField] private float curveStartDistance = 1.5f;
    [Tooltip("How quickly velocity eases into the orbit (higher = snappier).")]
    [SerializeField] private float settleSharpness = 2.0f;
    [Tooltip("How strongly it's pulled to the correct ring radius.")]
    [SerializeField] private float radialGain = 1.0f;
    [Tooltip("How strongly its height is held constant once orbiting.")]
    [SerializeField] private float verticalGain = 3.0f;

    private Rigidbody _rb;
    private Phase _phase = Phase.Idle;
    private bool _armed;
    private Vector3 _launchPoint;
    private float _orbitY;
    private float _targetRadius;

    private void Awake() => _rb = GetComponent<Rigidbody>();

    /// <summary>True while the bubble is settled into / circling its orbit.</summary>
    public bool IsOrbiting => _phase == Phase.Orbiting;

    private Vector3 Center
    {
        get
        {
            if (domeCenter != null) return domeCenter.position;
            if (ScenePropReference.Instance != null) return ScenePropReference.Instance.orbitCenter;
            return Vector3.zero;
        }
    }

    /// <summary>
    /// Call when the bubble is released/thrown (e.g. from the grab unselect event).
    /// Only launches if the release is an actual throw (see throwSpeedThreshold).
    /// </summary>
    public void OnReleased()
    {
        // Evaluate next FixedUpdate, once the grab system has applied the throw
        // velocity. Re-throwing an already-orbiting bubble re-orbits it too.
        if (_phase != Phase.Flying)
            _armed = true;
    }

    /// <summary>
    /// Put a loaded bubble straight into orbit, skipping the throw/fly phases.
    /// Treats the bubble's current position as a point already on the dome and
    /// holds its current height. Call this after instantiating a saved bubble.
    /// </summary>
    public void ResumeOrbit(bool isClockwise)
    {
        clockwise = isClockwise;
        _orbitY = transform.position.y;             // hold the saved elevation
        float h = _orbitY - Center.y;               // Center.y is the floor (0)
        float ring = Mathf.Sqrt(Mathf.Max(domeRadius * domeRadius - h * h, 0f));
        _targetRadius = Mathf.Max(ring, minRingRadius);
        _armed = false;
        _phase = Phase.Orbiting;
    }

    private void FixedUpdate()
    {
        if (_armed)
        {
            _armed = false;
            if (_rb.linearVelocity.magnitude >= throwSpeedThreshold)
            {
                if (randomizeDirection) clockwise = Random.value > 0.5f;
                _launchPoint = _rb.position;
                _phase = Phase.Flying;
            }
            else
            {
                _phase = Phase.Idle; // gently placed, not thrown
            }
        }

        if (_phase == Phase.Idle) return;

        Vector3 pos = _rb.position;
        Vector3 center = Center;

        if (_phase == Phase.Flying)
        {
            if (Vector3.Distance(_launchPoint, pos) >= curveStartDistance)
            {
                _orbitY = pos.y;                  // lock this bubble's elevation
                float h = _orbitY - center.y;     // height above the dome center
                float ring = Mathf.Sqrt(Mathf.Max(domeRadius * domeRadius - h * h, 0f));
                _targetRadius = Mathf.Max(ring, minRingRadius);
                _phase = Phase.Orbiting;
            }
            return; // keep flying straight
        }

        // ---- Orbiting ----
        Vector3 radial = pos - center;
        radial.y = 0f;
        float r = Mathf.Max(radial.magnitude, 0.001f);
        Vector3 radialDir = radial / r;

        Vector3 tangentDir = Vector3.Cross(Vector3.up, radialDir); // counter-clockwise from above
        if (clockwise) tangentDir = -tangentDir;

        Vector3 desiredVel =
            tangentDir * orbitSpeed                            // travel around the ring
            + radialDir * ((_targetRadius - r) * radialGain)   // settle to the ring radius
            + Vector3.up * ((_orbitY - pos.y) * verticalGain); // hold elevation constant

        _rb.linearVelocity = Vector3.Lerp(_rb.linearVelocity, desiredVel,
                                          settleSharpness * Time.fixedDeltaTime);
    }
}
