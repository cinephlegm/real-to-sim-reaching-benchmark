using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SvhJointMap))]
public class SvhHandColliderSetup : MonoBehaviour
{
    const string PhysicsBoxName = "_PhysicsBox";

    [Header("Physics material")]
    [Range(0f, 2f)] public float staticFriction = 1.2f;
    [Range(0f, 2f)] public float dynamicFriction = 0.9f;
    public PhysicMaterialCombine frictionCombine = PhysicMaterialCombine.Maximum;

    [Header("Emergency stability")]
    public bool disableBakedPhysicsBoxes = true;
    public bool disableExistingDirectSolidBoxes = true;
    public bool createMappedRuntimeBoxes = true;
    public bool ignoreMappedSelfCollisions = true;
    public bool validateCubeCollisionLayer = true;

    [Header("Palm collider")]
    public Vector3 palmSize = new Vector3(0.060f, 0.015f, 0.080f);
    public Vector3 palmCenter = new Vector3(0f, 0.008f, 0.010f);

    [Header("Finger colliders")]
    public Vector3 tipSize = new Vector3(0.014f, 0.008f, 0.018f);
    public Vector3 tipCenter = new Vector3(0f, 0f, 0.006f);
    public Vector3 proximalSize = new Vector3(0.012f, 0.012f, 0.028f);
    public Vector3 proximalCenter = new Vector3(0f, 0f, 0.010f);
    public Vector3 distalSize = new Vector3(0.011f, 0.011f, 0.022f);
    public Vector3 distalCenter = new Vector3(0f, 0f, 0.008f);

    [Header("Contact sensors")]
    public bool autoAddTriggerSensors = true;
    public string cubeTag = "GraspCube";

    static readonly (string link, string label)[] TipLinks =
    {
        ("svh_c", "ThumbPad"),
        ("svh_t", "IndexPad"),
        ("svh_s", "MiddlePad"),
        ("svh_r", "RingPad"),
        ("svh_q", "PinkyPad"),
    };

    SvhJointMap _map;

    void Awake()
    {
        _map = GetComponent<SvhJointMap>();
    }

    void Start()
    {
        Setup();
    }

    public void Setup()
    {
        if (_map == null)
            _map = GetComponent<SvhJointMap>();

        var mat = CreateMaterial();
        var monitor = GetComponentInChildren<CubeContactMonitor>(true) ?? FindObjectOfType<CubeContactMonitor>();
        var sensors = new List<FingerPadContactSensor>();
        var solidColliders = new List<Collider>();
        var bodies = GetComponentsInChildren<ArticulationBody>(true);

        int disabledBaked = disableBakedPhysicsBoxes ? DisableBakedPhysicsBoxes(bodies) : 0;
        int disabledExisting = disableExistingDirectSolidBoxes ? DisableExistingDirectSolidBoxes(bodies) : 0;
        int runtimeBoxes = createMappedRuntimeBoxes ? CreateMappedRuntimeBoxes(mat, solidColliders) : 0;

        if (autoAddTriggerSensors)
            SetupFingerSensors(monitor, sensors);

        foreach (var body in bodies)
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (ignoreMappedSelfCollisions)
            IgnoreSelfCollisions(solidColliders);

        if (monitor != null && validateCubeCollisionLayer)
            ValidateCubeLayer(monitor, solidColliders);

        Physics.defaultMaxDepenetrationVelocity = Mathf.Max(Physics.defaultMaxDepenetrationVelocity, 10f);

        Debug.Log($"[SvhHandColliderSetup] Stable mapped primitive mode. Disabled baked: {disabledBaked}, disabled direct old boxes: {disabledExisting}, mapped runtime boxes: {runtimeBoxes}, sensors: {sensors.Count}");
    }

    int DisableBakedPhysicsBoxes(ArticulationBody[] bodies)
    {
        int disabled = 0;

        foreach (var body in bodies)
        {
            if (body == null || !body.gameObject.name.StartsWith("svh_"))
                continue;

            var boxTransform = body.transform.Find(PhysicsBoxName);
            if (boxTransform == null)
                continue;

            foreach (var collider in boxTransform.GetComponentsInChildren<Collider>(true))
            {
                if (collider.enabled)
                {
                    collider.enabled = false;
                    disabled++;
                }
            }
        }

        return disabled;
    }

    int DisableExistingDirectSolidBoxes(ArticulationBody[] bodies)
    {
        int disabled = 0;

        foreach (var body in bodies)
        {
            if (body == null || !body.gameObject.name.StartsWith("svh_"))
                continue;

            foreach (var box in body.GetComponents<BoxCollider>())
            {
                if (!box.isTrigger && box.enabled)
                {
                    box.enabled = false;
                    disabled++;
                }
            }
        }

        return disabled;
    }

    int CreateMappedRuntimeBoxes(PhysicMaterial mat, List<Collider> solidColliders)
    {
        int count = 0;

        count += AddBox(FindDeep(transform, "svh_base_link"), palmSize, palmCenter, mat, solidColliders) ? 1 : 0;

        count += AddBox(_map.thumbFlexion, proximalSize, proximalCenter, mat, solidColliders) ? 1 : 0;
        count += AddBox(_map.thumbJ3, distalSize, distalCenter, mat, solidColliders) ? 1 : 0;
        count += AddBox(_map.thumbJ4, tipSize, tipCenter, mat, solidColliders) ? 1 : 0;

        count += AddBox(_map.indexProximal, proximalSize, proximalCenter, mat, solidColliders) ? 1 : 0;
        count += AddBox(_map.indexDistal, distalSize, distalCenter, mat, solidColliders) ? 1 : 0;
        count += AddBox(_map.indexJ14, tipSize, tipCenter, mat, solidColliders) ? 1 : 0;

        count += AddBox(_map.middleProximal, proximalSize, proximalCenter, mat, solidColliders) ? 1 : 0;
        count += AddBox(_map.middleDistal, distalSize, distalCenter, mat, solidColliders) ? 1 : 0;
        count += AddBox(_map.middleJ15, tipSize, tipCenter, mat, solidColliders) ? 1 : 0;

        count += AddBox(_map.ringBase, proximalSize, proximalCenter, mat, solidColliders) ? 1 : 0;
        count += AddBox(_map.ringJ12, distalSize, distalCenter, mat, solidColliders) ? 1 : 0;
        count += AddBox(_map.ringJ16, tipSize, tipCenter, mat, solidColliders) ? 1 : 0;

        count += AddBox(_map.pinkyBase, proximalSize, proximalCenter, mat, solidColliders) ? 1 : 0;
        count += AddBox(_map.pinkyJ13, distalSize, distalCenter, mat, solidColliders) ? 1 : 0;
        count += AddBox(_map.pinkyJ17, tipSize, tipCenter, mat, solidColliders) ? 1 : 0;

        return count;
    }

    bool AddBox(ArticulationBody body, Vector3 size, Vector3 center, PhysicMaterial mat, List<Collider> solidColliders)
    {
        if (body == null)
            return false;

        return AddBox(body.transform, size, center, mat, solidColliders);
    }

    bool AddBox(Transform target, Vector3 size, Vector3 center, PhysicMaterial mat, List<Collider> solidColliders)
    {
        if (target == null)
            return false;

        var box = target.gameObject.AddComponent<BoxCollider>();
        box.size = size;
        box.center = center;
        box.isTrigger = false;
        box.enabled = true;
        box.sharedMaterial = mat;
        solidColliders.Add(box);
        return true;
    }

    void IgnoreSelfCollisions(List<Collider> solidColliders)
    {
        for (int i = 0; i < solidColliders.Count; i++)
        {
            for (int j = i + 1; j < solidColliders.Count; j++)
            {
                if (solidColliders[i] != null && solidColliders[j] != null)
                    Physics.IgnoreCollision(solidColliders[i], solidColliders[j], true);
            }
        }
    }

    void SetupFingerSensors(CubeContactMonitor monitor, List<FingerPadContactSensor> sensors)
    {
        foreach (var (linkName, label) in TipLinks)
        {
            Transform link = FindDeep(transform, linkName);
            if (link == null)
                continue;

            string triggerName = label + "Trigger";
            Transform existing = link.Find(triggerName);
            if (existing != null)
            {
                var existingSensor = existing.GetComponent<FingerPadContactSensor>();
                if (existingSensor != null)
                {
                    existingSensor.monitor = monitor;
                    existingSensor.cubeTag = cubeTag;
                    sensors.Add(existingSensor);
                }
                continue;
            }

            var child = new GameObject(triggerName);
            child.transform.SetParent(link, false);
            child.layer = link.gameObject.layer;

            var trigger = child.AddComponent<BoxCollider>();
            trigger.size = Vector3.Max(tipSize + Vector3.one * 0.035f, new Vector3(0.060f, 0.060f, 0.060f));
            trigger.center = tipCenter;
            trigger.isTrigger = true;

            var sensor = child.AddComponent<FingerPadContactSensor>();
            sensor.cubeTag = cubeTag;
            sensor.monitor = monitor;
            sensors.Add(sensor);
        }

        if (monitor == null)
            return;

        monitor.fingerPads = sensors.ToArray();
        Transform palm = FindDeep(transform, "svh_base_link");
        if (palm != null && monitor.palm == null)
            monitor.palm = palm;
    }

    void ValidateCubeLayer(CubeContactMonitor monitor, List<Collider> solidColliders)
    {
        if (monitor.cubeCollider == null)
        {
            Debug.LogWarning("[SvhHandColliderSetup] CubeContactMonitor has no cubeCollider assigned. Cannot validate cube-hand collision layers.");
            return;
        }

        int cubeLayer = monitor.cubeCollider.gameObject.layer;
        var blockedLayers = new HashSet<int>();

        foreach (var handCollider in solidColliders)
        {
            if (handCollider == null)
                continue;

            int handLayer = handCollider.gameObject.layer;
            if (Physics.GetIgnoreLayerCollision(handLayer, cubeLayer))
                blockedLayers.Add(handLayer);

            if (Physics.GetIgnoreCollision(handCollider, monitor.cubeCollider))
                Debug.LogError($"[SvhHandColliderSetup] Direct hand-cube collision pair is ignored: {handCollider.name} vs {monitor.cubeCollider.name}");
        }

        if (blockedLayers.Count == 0)
            return;

        var names = new List<string>();
        foreach (int layer in blockedLayers)
            names.Add(string.IsNullOrEmpty(LayerMask.LayerToName(layer)) ? layer.ToString() : LayerMask.LayerToName(layer));

        Debug.LogError($"[SvhHandColliderSetup] Cube layer '{LayerMask.LayerToName(cubeLayer)}' does not collide with hand layer(s): {string.Join(", ", names)}. Fix Project Settings > Physics Layer Collision Matrix.");
    }

    PhysicMaterial CreateMaterial()
    {
        return new PhysicMaterial("HandPad_Runtime")
        {
            staticFriction = staticFriction,
            dynamicFriction = dynamicFriction,
            frictionCombine = frictionCombine,
            bounciness = 0f,
            bounceCombine = PhysicMaterialCombine.Minimum
        };
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            var found = FindDeep(child, name);
            if (found != null)
                return found;
        }

        return null;
    }
}
