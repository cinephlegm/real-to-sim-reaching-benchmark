using UnityEngine;

public class CubeStateDebugger : MonoBehaviour
{
    public Rigidbody cube;
    public Collider cubeCollider;
    public Transform expectedParent;
    public float insaneVelocityThreshold = 5f;
    public float fallenYThreshold = -0.25f;
    public bool logEveryChange = true;
    public bool freezeOnInsaneState;

    bool _lastKinematic;
    bool _lastUseGravity;
    bool _lastDetectCollisions;
    bool _lastColliderEnabled;
    bool _lastColliderTrigger;
    CollisionDetectionMode _lastCollisionMode;
    Transform _lastParent;
    bool _initialized;

    void Awake()
    {
        RefreshReferences();
        CaptureState();
    }

    void FixedUpdate()
    {
        RefreshReferences();

        if (cube == null)
            return;

        if (!_initialized)
            CaptureState();

        CheckStateChanges();
        CheckInsaneState();
    }

    public void RefreshReferences()
    {
        if (cube == null)
            cube = GetComponent<Rigidbody>();

        if (cubeCollider == null && cube != null)
        {
            cubeCollider = cube.GetComponent<Collider>();
            if (cubeCollider == null)
                cubeCollider = cube.GetComponentInChildren<Collider>();
        }

        if (cubeCollider == null)
            cubeCollider = GetComponent<Collider>();
    }

    void CaptureState()
    {
        if (cube == null)
            return;

        _lastKinematic = cube.isKinematic;
        _lastUseGravity = cube.useGravity;
        _lastDetectCollisions = cube.detectCollisions;
        _lastCollisionMode = cube.collisionDetectionMode;
        _lastParent = cube.transform.parent;
        _lastColliderEnabled = cubeCollider != null && cubeCollider.enabled;
        _lastColliderTrigger = cubeCollider != null && cubeCollider.isTrigger;
        _initialized = true;

        Debug.Log($"[CubeStateDebugger] Initial state: {FormatState()}");
    }

    void CheckStateChanges()
    {
        if (!logEveryChange)
            return;

        if (cube.isKinematic != _lastKinematic)
        {
            Debug.LogWarning($"[CubeStateDebugger] isKinematic changed {_lastKinematic} -> {cube.isKinematic}. {FormatState()}");
            _lastKinematic = cube.isKinematic;
        }

        if (cube.useGravity != _lastUseGravity)
        {
            Debug.LogWarning($"[CubeStateDebugger] useGravity changed {_lastUseGravity} -> {cube.useGravity}. {FormatState()}");
            _lastUseGravity = cube.useGravity;
        }

        if (cube.detectCollisions != _lastDetectCollisions)
        {
            Debug.LogWarning($"[CubeStateDebugger] detectCollisions changed {_lastDetectCollisions} -> {cube.detectCollisions}. {FormatState()}");
            _lastDetectCollisions = cube.detectCollisions;
        }

        if (cube.collisionDetectionMode != _lastCollisionMode)
        {
            Debug.LogWarning($"[CubeStateDebugger] collisionDetectionMode changed {_lastCollisionMode} -> {cube.collisionDetectionMode}. {FormatState()}");
            _lastCollisionMode = cube.collisionDetectionMode;
        }

        Transform currentParent = cube.transform.parent;
        if (currentParent != _lastParent)
        {
            string before = _lastParent == null ? "null" : _lastParent.name;
            string after = currentParent == null ? "null" : currentParent.name;
            Debug.LogWarning($"[CubeStateDebugger] parent changed {before} -> {after}. {FormatState()}");
            _lastParent = currentParent;
        }

        bool colliderEnabled = cubeCollider != null && cubeCollider.enabled;
        if (colliderEnabled != _lastColliderEnabled)
        {
            Debug.LogWarning($"[CubeStateDebugger] collider.enabled changed {_lastColliderEnabled} -> {colliderEnabled}. {FormatState()}");
            _lastColliderEnabled = colliderEnabled;
        }

        bool colliderTrigger = cubeCollider != null && cubeCollider.isTrigger;
        if (colliderTrigger != _lastColliderTrigger)
        {
            Debug.LogWarning($"[CubeStateDebugger] collider.isTrigger changed {_lastColliderTrigger} -> {colliderTrigger}. {FormatState()}");
            _lastColliderTrigger = colliderTrigger;
        }
    }

    void CheckInsaneState()
    {
        if (cubeCollider == null)
            Debug.LogError("[CubeStateDebugger] Cube has no Collider reference.");
        else if (!cubeCollider.enabled)
            Debug.LogError($"[CubeStateDebugger] Cube collider is disabled. {FormatState()}");
        else if (cubeCollider.isTrigger)
            Debug.LogError($"[CubeStateDebugger] Cube collider is trigger. {FormatState()}");

        if (!cube.detectCollisions)
            Debug.LogError($"[CubeStateDebugger] Cube detectCollisions is false. {FormatState()}");

        if (cube.isKinematic && cube.useGravity)
            Debug.LogError($"[CubeStateDebugger] Cube is kinematic while gravity is enabled. {FormatState()}");

        if (cube.velocity.magnitude >= insaneVelocityThreshold)
            Debug.LogError($"[CubeStateDebugger] Cube insane velocity {cube.velocity.magnitude:F3}. velocity={cube.velocity} angular={cube.angularVelocity}. {FormatState()}");

        if (cube.position.y <= fallenYThreshold)
            Debug.LogError($"[CubeStateDebugger] Cube fell below threshold y={cube.position.y:F3}. {FormatState()}");

        if (expectedParent != null && cube.transform.parent != expectedParent)
            Debug.LogWarning($"[CubeStateDebugger] Cube parent is not expected parent. current={(cube.transform.parent == null ? "null" : cube.transform.parent.name)} expected={expectedParent.name}");

        if (freezeOnInsaneState && (cube.velocity.magnitude >= insaneVelocityThreshold || cube.position.y <= fallenYThreshold))
            Debug.Break();
    }

    string FormatState()
    {
        string col = cubeCollider == null ? "null" : $"{cubeCollider.GetType().Name}(enabled={cubeCollider.enabled}, trigger={cubeCollider.isTrigger}, layer={LayerMask.LayerToName(cubeCollider.gameObject.layer)})";
        string parent = cube == null || cube.transform.parent == null ? "null" : cube.transform.parent.name;
        if (cube == null)
            return "cube=null";

        return $"pos={cube.position} vel={cube.velocity} ang={cube.angularVelocity} kin={cube.isKinematic} grav={cube.useGravity} detect={cube.detectCollisions} mode={cube.collisionDetectionMode} sleep={cube.IsSleeping()} parent={parent} collider={col}";
    }
}
