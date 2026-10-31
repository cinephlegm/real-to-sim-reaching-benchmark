using UnityEngine;

public class SvhSynergyHandDriver : MonoBehaviour
{
    public SvhJointMap map;

    [Range(0f, 1f)] public float handClose;
    [Range(0f, 1f)] public float thumbOppose;
    [Range(0f, 1f)] public float fingerSpread;

    public float stiffness = 400f;
    public float damping = 40f;
    public float forceLimit = 20f;  // 80 was enough to bulldoze through a cube — 20 N is realistic

    public bool setRootImmovable = true;
    public bool disableGravity = true;

    void Awake()
    {
        if (setRootImmovable)
        {
            var bodies = GetComponentsInChildren<ArticulationBody>(true);
            foreach (var body in bodies)
            {
                if (disableGravity)
                    body.useGravity = false;

                if (body.isRoot)
                    body.immovable = true;
            }
        }

        ConfigureJoint(map.thumbOpposition);
        ConfigureJoint(map.thumbFlexion);
        ConfigureJoint(map.thumbJ3);
        ConfigureJoint(map.thumbJ4);
        ConfigureJoint(map.thumbJ5);

        ConfigureJoint(map.indexProximal);
        ConfigureJoint(map.indexDistal);
        ConfigureJoint(map.indexJ14);

        ConfigureJoint(map.middleProximal);
        ConfigureJoint(map.middleDistal);
        ConfigureJoint(map.middleJ15);

        ConfigureJoint(map.ringBase);
        ConfigureJoint(map.ringJ12);
        ConfigureJoint(map.ringJ16);

        ConfigureJoint(map.pinkyBase);
        ConfigureJoint(map.pinkyJ13);
        ConfigureJoint(map.pinkyJ17);

        ConfigureJoint(map.fingerSpread);
        ConfigureJoint(map.indexSpread);
        ConfigureJoint(map.ringSpread);

        Apply();
    }

    public void SetSynergies(float close01, float oppose01, float spread01)
    {
        handClose = Mathf.Clamp01(close01);
        thumbOppose = Mathf.Clamp01(oppose01);
        fingerSpread = Mathf.Clamp01(spread01);
        Apply();
    }

    /// <summary>
    /// Drive each finger independently from MediaPipe per-finger values.
    /// thumb/index/middle/ring/pinky are flexion [0=open, 1=closed].
    /// spread01 is finger spread [0=together, 1=spread].
    /// </summary>
    public void SetFingers(float thumb, float index, float middle, float ring, float pinky, float spread01)
    {
        thumb = Mathf.Clamp01(thumb);
        index = Mathf.Clamp01(index);
        middle = Mathf.Clamp01(middle);
        ring = Mathf.Clamp01(ring);
        pinky = Mathf.Clamp01(pinky);
        spread01 = Mathf.Clamp01(spread01);

        // Thumb: curl drives both flexion and opposition together
        SetTargetRad(map.thumbOpposition, thumb * 1.36f);
        SetTargetRad(map.thumbJ5, thumb * 0.98786f);
        SetTargetRad(map.thumbFlexion, thumb * 0.9704f);
        SetTargetRad(map.thumbJ3, thumb * 0.9704f * 1.01511f);
        SetTargetRad(map.thumbJ4, thumb * 0.9704f * 1.44889f);

        // Index finger
        SetTargetRad(map.indexProximal, index * 0.79849f);
        SetTargetRad(map.indexDistal, index * 1.334f);
        SetTargetRad(map.indexJ14, index * 1.334f * 1.0450f);

        // Middle finger
        SetTargetRad(map.middleProximal, middle * 0.79849f);
        SetTargetRad(map.middleDistal, middle * 1.334f);
        SetTargetRad(map.middleJ15, middle * 1.334f * 1.0454f);

        // Ring finger
        SetTargetRad(map.ringBase, ring * 0.98175f);
        SetTargetRad(map.ringJ12, ring * 0.98175f * 1.35880f);
        SetTargetRad(map.ringJ16, ring * 0.98175f * 1.42093f);

        // Pinky finger
        SetTargetRad(map.pinkyBase, pinky * 0.98175f);
        SetTargetRad(map.pinkyJ13, pinky * 0.98175f * 1.35880f);
        SetTargetRad(map.pinkyJ17, pinky * 0.98175f * 1.42307f);

        // Spread
        SetTargetRad(map.fingerSpread, spread01 * 0.5829f);
        SetTargetRad(map.indexSpread, spread01 * 0.5829f * 0.5f);
        SetTargetRad(map.ringSpread, spread01 * 0.5829f * 0.5f);

        // Keep legacy fields updated so existing observers (e.g. release-check) still work
        handClose = (index + middle + ring + pinky) * 0.25f;
        thumbOppose = thumb;
        fingerSpread = spread01;
    }

    public void Apply()
    {
        SetTargetRad(map.thumbOpposition, thumbOppose * 1.36f);
        SetTargetRad(map.thumbJ5, thumbOppose * 0.98786f);
        SetTargetRad(map.thumbFlexion, handClose * 0.9704f);
        SetTargetRad(map.thumbJ3, handClose * 0.9704f * 1.01511f);
        SetTargetRad(map.thumbJ4, handClose * 0.9704f * 1.44889f);

        SetTargetRad(map.indexProximal, handClose * 0.79849f);
        SetTargetRad(map.indexDistal, handClose * 1.334f);
        SetTargetRad(map.indexJ14, handClose * 1.334f * 1.0450f);

        SetTargetRad(map.middleProximal, handClose * 0.79849f);
        SetTargetRad(map.middleDistal, handClose * 1.334f);
        SetTargetRad(map.middleJ15, handClose * 1.334f * 1.0454f);

        SetTargetRad(map.ringBase, handClose * 0.98175f);
        SetTargetRad(map.ringJ12, handClose * 0.98175f * 1.35880f);
        SetTargetRad(map.ringJ16, handClose * 0.98175f * 1.42093f);

        SetTargetRad(map.pinkyBase, handClose * 0.98175f);
        SetTargetRad(map.pinkyJ13, handClose * 0.98175f * 1.35880f);
        SetTargetRad(map.pinkyJ17, handClose * 0.98175f * 1.42307f);

        SetTargetRad(map.fingerSpread, fingerSpread * 0.5829f);
        SetTargetRad(map.indexSpread, fingerSpread * 0.5829f * 0.5f);
        SetTargetRad(map.ringSpread, fingerSpread * 0.5829f * 0.5f);
    }

    void ConfigureJoint(ArticulationBody joint)
    {
        if (joint == null) return;

        var drive = joint.xDrive;
        drive.stiffness = stiffness;
        drive.damping = damping;
        drive.forceLimit = forceLimit;
        drive.targetVelocity = 0f;

        float currentDeg = 0f;
        if (joint.jointPosition.dofCount > 0)
            currentDeg = joint.jointPosition[0] * Mathf.Rad2Deg;

        drive.target = Mathf.Clamp(currentDeg, drive.lowerLimit, drive.upperLimit);
        joint.xDrive = drive;
    }

    void SetTargetRad(ArticulationBody joint, float targetRad)
    {
        if (joint == null) return;

        float targetDeg = targetRad * Mathf.Rad2Deg;
        var drive = joint.xDrive;
        drive.target = Mathf.Clamp(targetDeg, drive.lowerLimit, drive.upperLimit);
        joint.xDrive = drive;
    }

    // ------------------------------------------------------------------ //
    //  Teleport — physically snap hand joints to their current xDrive targets
    // ------------------------------------------------------------------ //

    /// <summary>
    /// After calling SetSynergies / SetFingers / Apply, call this to physically
    /// teleport all hand joints to those targets instantly.
    /// Without this, the PD controller slowly drives toward the target over many frames.
    /// </summary>
    public void TeleportToCurrentTargets()
    {
        // Find the root ArticulationBody of the hand hierarchy.
        ArticulationBody root = null;
        foreach (var b in GetComponentsInChildren<ArticulationBody>(true))
        {
            if (b.isRoot) { root = b; break; }
        }
        if (root == null) return;

        // Build a flat position list that matches GetJointPositions order (depth-first).
        var ordered = new System.Collections.Generic.List<(ArticulationBody body, int dofStart)>();
        int counter = 0;
        CollectHandDofOrder(root, ordered, ref counter);

        var positions = new System.Collections.Generic.List<float>(new float[counter]);
        foreach (var (body, dofStart) in ordered)
        {
            for (int d = 0; d < body.jointPosition.dofCount; d++)
            {
                // xDrive.target is in degrees for revolute joints → convert to radians.
                float targetRad = (d == 0) ? body.xDrive.target * Mathf.Deg2Rad : 0f;
                if (dofStart + d < positions.Count)
                    positions[dofStart + d] = targetRad;
            }
        }

        if (positions.Count == 0) return;

        var zeros = new System.Collections.Generic.List<float>(new float[positions.Count]);
        root.SetJointPositions(positions);
        root.SetJointVelocities(zeros);
    }

    void CollectHandDofOrder(ArticulationBody body,
                             System.Collections.Generic.List<(ArticulationBody, int)> list,
                             ref int idx)
    {
        if (!body.isRoot)
        {
            list.Add((body, idx));
            idx += body.jointPosition.dofCount;
        }
        foreach (Transform child in body.transform)
        {
            var ab = child.GetComponent<ArticulationBody>();
            if (ab != null && !ab.isRoot)  // don't cross into other articulation trees
                CollectHandDofOrder(ab, list, ref idx);
        }
    }
}
