using UnityEngine;

/// <summary>
/// Detects whether this finger-pad volume is touching the grasp cube.
///
/// Uses Physics.OverlapBox in FixedUpdate instead of OnTriggerEnter because
/// Unity does not reliably dispatch trigger events to child GameObjects whose
/// parent has an ArticulationBody (events go to the AB's own GameObject only).
/// OverlapBox polling is deterministic and AB-safe.
/// </summary>
public class FingerPadContactSensor : MonoBehaviour
{
    public CubeContactMonitor monitor;
    public string cubeTag = "GraspCube";

    public bool IsTouching { get; private set; }
    public Collider CurrentCubeCollider { get; private set; }

    // The BoxCollider on this GameObject drives the overlap query.
    BoxCollider _box;

    // Expand the overlap box slightly so we detect contact before
    // the solid finger collider's physics response pushes objects apart.
    const float ExpandBy = 0.004f;

    static readonly Collider[] _hits = new Collider[4];

    void Awake()
    {
        _box = GetComponent<BoxCollider>();
    }

    void FixedUpdate()
    {
        if (_box == null) { IsTouching = false; return; }

        Vector3 halfExt = _box.size * 0.5f + Vector3.one * ExpandBy;
        Vector3 worldCenter = transform.TransformPoint(_box.center);
        int count = Physics.OverlapBoxNonAlloc(worldCenter, halfExt,
                                               _hits, transform.rotation,
                                               ~0, QueryTriggerInteraction.Ignore);

        bool hit = false;
        for (int n = 0; n < count; n++)
        {
            Collider c = _hits[n];
            if (c == null) continue;

            // Match by direct reference first (fastest), then by tag.
            if ((monitor != null && monitor.cubeCollider != null && c == monitor.cubeCollider)
                || c.CompareTag(cubeTag))
            {
                CurrentCubeCollider = c;
                hit = true;
                break;
            }
        }

        if (!hit) CurrentCubeCollider = null;
        IsTouching = hit;
    }
}
