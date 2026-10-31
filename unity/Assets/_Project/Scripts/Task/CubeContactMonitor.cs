using System.Collections.Generic;
using UnityEngine;

public class CubeContactMonitor : MonoBehaviour
{
    public Rigidbody cubeRigidbody;
    public Collider cubeCollider;
    public Transform palm;
    public FingerPadContactSensor[] fingerPads;

    [Header("Cube physics setup")]
    public bool applySettingsOnStart = true;
    public bool keepAwakeDuringPlay = true;
    public bool rebuildColliderOnStart = true;
    public bool forceEnableCubeCollider = true;
    public bool forceCubeColliderSolid = true;
    public CollisionDetectionMode collisionMode = CollisionDetectionMode.ContinuousDynamic;
    public float sleepThreshold = 0f;
    public int solverIterations = 12;
    public int solverVelocityIterations = 4;
    public float maxDepenetrationVelocity = 2f;

    void Awake()
    {
        RefreshReferences();
    }

    void Start()
    {
        if (!applySettingsOnStart)
            return;

        ApplyCubePhysicsSettings(rebuildColliderOnStart);
    }

    void FixedUpdate()
    {
        if (keepAwakeDuringPlay && cubeRigidbody != null)
            cubeRigidbody.WakeUp();
    }

    public void RefreshReferences()
    {
        if (cubeRigidbody == null)
            cubeRigidbody = GetComponent<Rigidbody>();

        if (cubeRigidbody != null && cubeCollider == null)
        {
            cubeCollider = cubeRigidbody.GetComponent<Collider>();
            if (cubeCollider == null)
                cubeCollider = cubeRigidbody.GetComponentInChildren<Collider>();
        }

        if (cubeCollider == null)
            cubeCollider = GetComponent<Collider>();
    }

    public void ApplyCubePhysicsSettings(bool rebuildCollider)
    {
        RefreshReferences();

        if (cubeRigidbody == null)
        {
            Debug.LogError("[CubeContactMonitor] Missing cube Rigidbody.");
            return;
        }

        if (cubeCollider == null)
        {
            Debug.LogError("[CubeContactMonitor] Missing cube Collider. Cube will fall through everything.");
            return;
        }

        cubeRigidbody.detectCollisions = true;
        cubeRigidbody.isKinematic = false;
        cubeRigidbody.collisionDetectionMode = collisionMode;
        cubeRigidbody.sleepThreshold = sleepThreshold;
        cubeRigidbody.solverIterations = solverIterations;
        cubeRigidbody.solverVelocityIterations = solverVelocityIterations;
        cubeRigidbody.maxDepenetrationVelocity = maxDepenetrationVelocity;

        ForceCubeColliderUsable();

        if (rebuildCollider)
            RebuildCubeColliderContactState();

        ForceCubeColliderUsable();
        Physics.SyncTransforms();
        cubeRigidbody.WakeUp();
    }

    public void RebuildCubeColliderContactState()
    {
        RefreshReferences();

        if (cubeRigidbody == null || cubeCollider == null)
            return;

        bool detectWasEnabled = cubeRigidbody.detectCollisions;
        CollisionDetectionMode desiredMode = collisionMode;

        cubeCollider.enabled = false;
        cubeRigidbody.detectCollisions = false;
        Physics.SyncTransforms();

        cubeRigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        cubeRigidbody.collisionDetectionMode = desiredMode;
        cubeRigidbody.detectCollisions = detectWasEnabled;
        ForceCubeColliderUsable();

        Physics.SyncTransforms();
        cubeRigidbody.WakeUp();
    }

    void ForceCubeColliderUsable()
    {
        if (cubeCollider == null)
            return;

        if (forceEnableCubeCollider)
            cubeCollider.enabled = true;

        if (forceCubeColliderSolid)
            cubeCollider.isTrigger = false;
    }

    public int TouchingPadCount
    {
        get
        {
            int count = 0;
            if (fingerPads == null) return 0;

            for (int i = 0; i < fingerPads.Length; i++)
            {
                if (fingerPads[i] != null && fingerPads[i].IsTouching)
                    count++;
            }

            return count;
        }
    }

    public float CubeSpeed
    {
        get
        {
            if (cubeRigidbody == null) return 0f;
            return cubeRigidbody.velocity.magnitude;
        }
    }

    public float PalmDistanceToCube
    {
        get
        {
            if (palm == null || cubeRigidbody == null) return -1f;
            return Vector3.Distance(palm.position, cubeRigidbody.worldCenterOfMass);
        }
    }

    public bool IsTouching(string sensorObjectName)
    {
        if (fingerPads == null) return false;

        for (int i = 0; i < fingerPads.Length; i++)
        {
            if (fingerPads[i] == null) continue;
            if (fingerPads[i].gameObject.name == sensorObjectName)
                return fingerPads[i].IsTouching;
        }

        return false;
    }

    public string GetTouchingPadNames()
    {
        if (fingerPads == null || fingerPads.Length == 0)
            return "None";

        List<string> names = new List<string>();

        for (int i = 0; i < fingerPads.Length; i++)
        {
            if (fingerPads[i] != null && fingerPads[i].IsTouching)
                names.Add(fingerPads[i].gameObject.name);
        }

        return names.Count == 0 ? "None" : string.Join(", ", names);
    }
}
