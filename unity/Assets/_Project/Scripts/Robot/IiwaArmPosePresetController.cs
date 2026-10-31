using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Snaps the iiwa7 arm to saved joint-angle presets via keypress.
///
/// IMPORTANT — demo recording rule:
///   Presets bypass the agent action buffer (they set xDrive directly).
///   Only fire a preset at the START of an episode (during reset, before the
///   DemonstrationRecorder captures that episode). Never mid-episode.
///   Mid-episode arm movement must use keyboard through the agent Heuristic().
///
/// Usage:
///   1. Manually move the arm with ArticulationJointTester to a good pose.
///   2. Click "Capture → PreGrasp" (or Home / Lift) in the Inspector.
///   3. During recording: press 1/2/3 only at episode start to seed the pose,
///      then drive the rest with normal keyboard teleop through the agent.
/// </summary>
public class IiwaArmPosePresetController : MonoBehaviour
{
    [Tooltip("The 7 iiwa ArticulationBody joints in order (joint_1 … joint_7). " +
             "Drag from the robot hierarchy.")]
    public ArticulationBody[] armJoints = new ArticulationBody[7];

    [Header("Preset Keys (New Input System)")]
    public Key homePoseKey = Key.Digit1;
    public Key preGraspPoseKey = Key.Digit2;
    public Key liftPoseKey = Key.Digit3;
    public Key placePoseKey = Key.Digit4;

    [Header("Safety")]
    [Tooltip("Enable during demo recording so preset keys are active. Keep false for inference/training.")]
    public bool allowRuntimePresetKeys = false;

    [Header("BC Recording — Heuristic-Driven Mode")]
    [Tooltip("TRUE during demo recording: preset keys set a target that Heuristic() chases via the action buffer (valid BC data). "
             + "FALSE: preset keys directly write xDrive (setup/debug only, NOT valid for BC).")]
    public bool heuristicDrivenMode = true;
    [Tooltip("All joints must be within this many degrees of the preset target to consider 'arrived'.")]
    public float arrivalThresholdDeg = 1.5f;

    // Exposed for DexterousPickPlaceAgent.Heuristic()
    public bool IsMovingToPreset { get; private set; }
    public float[] ActivePresetDeg { get; private set; }

    [Header("Smooth Interpolation (non-heuristic mode only)")]
    public bool smoothMove = true;
    public float moveDuration = 0.6f;

    [Header("Saved Poses (degrees)")]
    public float[] homePoseDeg = new float[7];
    public float[] preGraspPoseDeg = new float[7];
    public float[] liftPoseDeg = new float[7];
    public float[] placePoseDeg = new float[7];

    Coroutine _moveRoutine;

    // ------------------------------------------------------------------ //
    //  Runtime key handling
    // ------------------------------------------------------------------ //

    bool _triggeredThisFrame;

    void Update()
    {
        if (!allowRuntimePresetKeys) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        _triggeredThisFrame = false;
        if (kb[homePoseKey].wasPressedThisFrame) { TriggerPreset(homePoseDeg); _triggeredThisFrame = true; }
        if (kb[preGraspPoseKey].wasPressedThisFrame) { TriggerPreset(preGraspPoseDeg); _triggeredThisFrame = true; }
        if (kb[liftPoseKey].wasPressedThisFrame) { TriggerPreset(liftPoseDeg); _triggeredThisFrame = true; }
        if (kb[placePoseKey].wasPressedThisFrame) { TriggerPreset(placePoseDeg); _triggeredThisFrame = true; }

        // Auto-clear once arm has arrived — but NEVER on the same frame we just triggered,
        // otherwise CheckArrival fires before FixedUpdate/Heuristic even sees the flag.
        if (heuristicDrivenMode && IsMovingToPreset && !_triggeredThisFrame)
            CheckArrival();
    }

    void TriggerPreset(float[] preset)
    {
        if (preset == null || preset.Length != 7) return;

        if (heuristicDrivenMode)
        {
            // Store target; Heuristic() drives the arm through the action buffer — valid BC data.
            ActivePresetDeg = preset;
            IsMovingToPreset = true;
            // Stop any direct-write coroutine that may be running from a previous non-heuristic call
            if (_moveRoutine != null) { StopCoroutine(_moveRoutine); _moveRoutine = null; }
        }
        else
        {
            // Setup/debug only — writes xDrive directly, NOT valid during recording
            ApplyPreset(preset);
        }
    }

    void CheckArrival()
    {
        if (ActivePresetDeg == null || !IsValid()) return;
        for (int i = 0; i < 7; i++)
        {
            float current = armJoints[i].jointPosition.dofCount > 0
                ? armJoints[i].jointPosition[0] * Mathf.Rad2Deg
                : armJoints[i].xDrive.target;
            if (Mathf.Abs(current - ActivePresetDeg[i]) > arrivalThresholdDeg) return;
        }
        IsMovingToPreset = false;
    }

    /// <summary>Called by DexterousPickPlaceAgent.OnEpisodeBegin to snap arm to home without recording.</summary>
    public void SnapToHomeImmediate()
    {
        Debug.Log($"[ArmPreset:{name}] SnapToHomeImmediate() — homePoseDeg:[{string.Join(", ", System.Array.ConvertAll(homePoseDeg, d => d.ToString("F1")))}]");
        if (_moveRoutine != null) { StopCoroutine(_moveRoutine); _moveRoutine = null; }
        IsMovingToPreset = false;

        // Zero all joint velocities so residual momentum doesn't carry over into the new episode.
        if (armJoints != null)
        {
            foreach (var j in armJoints)
            {
                if (j == null) continue;
                j.velocity = Vector3.zero;
                j.angularVelocity = Vector3.zero;
            }
        }

        ApplyImmediate(homePoseDeg);

        // Physically teleport joints to the target positions so the arm doesn't just
        // slowly PD-chase the target — it starts the new episode exactly at home.
        TeleportJointsToTargets(homePoseDeg);
        Debug.Log($"[ArmPreset:{name}] SnapToHomeImmediate() DONE.");
    }

    // ------------------------------------------------------------------ //
    //  Capture current pose into a slot  (call from Inspector buttons or code)
    // ------------------------------------------------------------------ //

    public void CaptureCurrentToHome() => CaptureCurrentPose(homePoseDeg);
    public void CaptureCurrentToPreGrasp() => CaptureCurrentPose(preGraspPoseDeg);
    public void CaptureCurrentToLift() => CaptureCurrentPose(liftPoseDeg);
    public void CaptureCurrentToPlace() => CaptureCurrentPose(placePoseDeg);

    void CaptureCurrentPose(float[] target)
    {
        if (!IsValid()) return;

        for (int i = 0; i < 7; i++)
        {
            var j = armJoints[i];
            // prefer live joint position; fall back to drive target
            target[i] = j.jointPosition.dofCount > 0
                ? j.jointPosition[0] * Mathf.Rad2Deg
                : j.xDrive.target;
        }

        Debug.Log("[IiwaPreset] Pose captured.");
    }

    // ------------------------------------------------------------------ //
    //  Apply
    // ------------------------------------------------------------------ //

    public void ApplyPreset(float[] preset)
    {
        if (!IsValid() || preset == null || preset.Length != 7) return;

        if (_moveRoutine != null) StopCoroutine(_moveRoutine);

        _moveRoutine = smoothMove
            ? StartCoroutine(SmoothMove(preset))
            : null;

        if (!smoothMove) ApplyImmediate(preset);
    }

    void ApplyImmediate(float[] preset)
    {
        if (!IsValid())
        {
            Debug.LogError($"[ArmPreset:{name}] ApplyImmediate() — IsValid() returned FALSE! armJoints may be null or wrong length.");
            return;
        }
        for (int i = 0; i < 7; i++)
        {
            var drive = armJoints[i].xDrive;
            drive.target = Mathf.Clamp(preset[i], drive.lowerLimit, drive.upperLimit);
            armJoints[i].xDrive = drive;
        }
        Debug.Log($"[ArmPreset:{name}] ApplyImmediate() applied — joint[0] target={armJoints[0].xDrive.target:F2}");
    }

    IEnumerator SmoothMove(float[] preset)
    {
        float[] start = new float[7];
        for (int i = 0; i < 7; i++)
            start[i] = armJoints[i].xDrive.target;

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / moveDuration);

            for (int i = 0; i < 7; i++)
            {
                var drive = armJoints[i].xDrive;
                drive.target = Mathf.Clamp(
                    Mathf.Lerp(start[i], preset[i], alpha),
                    drive.lowerLimit, drive.upperLimit);
                armJoints[i].xDrive = drive;
            }

            yield return null;
        }

        ApplyImmediate(preset);
        _moveRoutine = null;
    }

    // ------------------------------------------------------------------ //
    //  Teleport helpers — physically move joints, not just set PD targets
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Instantly teleports each arm joint to <paramref name="poseDeg"/> by writing
    /// directly into Unity's articulation position state (SetJointPositions / SetJointVelocities).
    /// Must be called AFTER ApplyImmediate so xDrive.target is already set.
    /// </summary>
    void TeleportJointsToTargets(float[] poseDeg)
    {
        if (!IsValid()) return;

        // Walk up from joint[0] to find the root ArticulationBody of this chain.
        var parents = armJoints[0].GetComponentsInParent<ArticulationBody>();
        var root = parents[parents.Length - 1];

        // Get the current flat position list (size = total DOFs in this articulation tree).
        var positions = new System.Collections.Generic.List<float>();
        root.GetJointPositions(positions);

        // Build a map: each ArticulationBody → its first DOF index in the flat list.
        // Uses the same depth-first order Unity uses internally.
        var dofMap = new System.Collections.Generic.Dictionary<ArticulationBody, int>();
        int counter = 0;
        BuildDofIndexMap(root, dofMap, ref counter);

        // Overwrite the entries that correspond to our 7 arm joints.
        for (int i = 0; i < 7; i++)
        {
            if (dofMap.TryGetValue(armJoints[i], out int dofIdx) && dofIdx < positions.Count)
                positions[dofIdx] = poseDeg[i] * Mathf.Deg2Rad;
            else
                Debug.LogWarning($"[ArmPreset:{name}] TeleportJoints: armJoints[{i}] not in DOF map (totalDOFs={positions.Count})");
        }

        var zeros = new System.Collections.Generic.List<float>(new float[positions.Count]);
        root.SetJointPositions(positions);
        root.SetJointVelocities(zeros);
        Debug.Log($"[ArmPreset:{name}] TeleportJoints: root={root.name} totalDOFs={positions.Count} joint[0]={positions[0] * Mathf.Rad2Deg:F1}°");
    }

    /// <summary>Depth-first traversal that maps each non-root ArticulationBody to its
    /// starting index in the flat DOF list produced by GetJointPositions.</summary>
    void BuildDofIndexMap(ArticulationBody body,
                          System.Collections.Generic.Dictionary<ArticulationBody, int> map,
                          ref int idx)
    {
        if (!body.isRoot)
        {
            map[body] = idx;
            idx += body.jointPosition.dofCount;
        }
        foreach (Transform child in body.transform)
        {
            var ab = child.GetComponent<ArticulationBody>();
            if (ab != null && !ab.isRoot)   // don't cross into separate articulation trees
                BuildDofIndexMap(ab, map, ref idx);
        }
    }

    // ------------------------------------------------------------------ //
    //  Helpers
    // ------------------------------------------------------------------ //

    bool IsValid() => armJoints != null && armJoints.Length == 7
                   && armJoints[0] != null;

    // HUD merged into DexterousPickPlaceAgent unified panel.
    // Set showHUD = true here if you need this panel standalone.
    public bool showHUD = false;

    void OnGUI()
    {
        if (!showHUD) return;
        string state = IsMovingToPreset ? ">> Moving <<" : (allowRuntimePresetKeys ? "Ready" : "LOCKED");
        string modeStr = heuristicDrivenMode ? "BC" : "Direct";
        GUILayout.BeginArea(new Rect(10, 10, 420, 42), GUI.skin.box);
        GUILayout.Label($"Presets [{modeStr}]  1=Home 2=PreGrasp 3=Lift 4=Place");
        GUILayout.Label(state);
        GUILayout.EndArea();
    }
}
