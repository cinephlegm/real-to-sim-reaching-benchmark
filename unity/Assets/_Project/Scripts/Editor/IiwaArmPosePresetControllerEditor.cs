using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Inspector for IiwaArmPosePresetController.
///
/// Adds three sections:
///   1. Auto-Wire     — finds the 7 iiwa_joint_X ArticulationBodies automatically.
///   2. Capture Poses — captures current joint angles into a preset slot while in Play mode.
///   3. PIE Persistence — poses captured during Play mode are saved to EditorPrefs
///                        and restored automatically when you stop Play mode.
/// </summary>
[CustomEditor(typeof(IiwaArmPosePresetController))]
public class IiwaArmPosePresetControllerEditor : Editor
{
    // EditorPrefs key prefix — unique per scene object instance ID
    const string PrefKey = "IiwaPreset_";

    // ------------------------------------------------------------------ //
    //  PIE persistence wiring — subscribe once on load
    // ------------------------------------------------------------------ //

    [InitializeOnLoadMethod]
    static void RegisterPlayModeHook()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // ExitingPlayMode fires while Play mode values are still live.
        // We save them to EditorPrefs here.
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            var all = FindObjectsByType<IiwaArmPosePresetController>(FindObjectsSortMode.None);
            foreach (var ctrl in all)
                SaveToPrefs(ctrl);
        }

        // EnteredEditMode fires after Unity has reverted serialized values.
        // We restore from EditorPrefs here.
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            var all = FindObjectsByType<IiwaArmPosePresetController>(FindObjectsSortMode.None);
            foreach (var ctrl in all)
                RestoreFromPrefs(ctrl);
        }
    }

    // ------------------------------------------------------------------ //
    //  Inspector GUI
    // ------------------------------------------------------------------ //

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var ctrl = (IiwaArmPosePresetController)target;

        // ---- 1. Auto-Wire ----
        GUILayout.Space(10);
        EditorGUILayout.LabelField("── Auto-Wire ──────────────────────", EditorStyles.boldLabel);

        if (GUILayout.Button("Find iiwa Joints from Hierarchy"))
            AutoWireJoints(ctrl);

        // ---- 2. Capture Poses ----
        GUILayout.Space(6);
        EditorGUILayout.LabelField("── Capture Poses ───────────────────", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play mode and position the arm, then click Capture.",
                MessageType.Info);
        }

        GUI.enabled = Application.isPlaying && ctrl.armJoints != null && ctrl.armJoints.Length == 7;

        if (GUILayout.Button("Capture → Home Pose"))
        {
            Undo.RecordObject(ctrl, "Capture Home Pose");
            ctrl.CaptureCurrentToHome();
            Debug.Log("[IiwaPreset] Home pose captured.");
        }

        if (GUILayout.Button("Capture → Pre-Grasp Pose"))
        {
            Undo.RecordObject(ctrl, "Capture PreGrasp Pose");
            ctrl.CaptureCurrentToPreGrasp();
            Debug.Log("[IiwaPreset] Pre-grasp pose captured.");
        }

        if (GUILayout.Button("Capture → Lift Pose"))
        {
            Undo.RecordObject(ctrl, "Capture Lift Pose");
            ctrl.CaptureCurrentToLift();
            Debug.Log("[IiwaPreset] Lift pose captured.");
        }

        if (GUILayout.Button("Capture → Place Pose"))
        {
            Undo.RecordObject(ctrl, "Capture Place Pose");
            ctrl.CaptureCurrentToPlace();
            Debug.Log("[IiwaPreset] Place pose captured.");
        }

        GUI.enabled = true;

        // ---- 3. Show current saved values ----
        GUILayout.Space(6);
        EditorGUILayout.LabelField("── Saved Values (read-only preview) ─", EditorStyles.boldLabel);
        DrawPosePreview("Home", ctrl.homePoseDeg);
        DrawPosePreview("PreGrasp", ctrl.preGraspPoseDeg);
        DrawPosePreview("Lift", ctrl.liftPoseDeg);
        DrawPosePreview("Place", ctrl.placePoseDeg);

        // ---- 4. Manual persist button ----
        GUILayout.Space(6);
        EditorGUILayout.LabelField("── PIE Persistence ────────────────", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Poses captured in Play mode are saved automatically when you stop Play.\n" +
            "Use the button below if you want to force-save mid-session.",
            MessageType.None);

        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("Force-Save Poses to Prefs NOW"))
            SaveToPrefs(ctrl);
        GUI.enabled = true;

        GUI.enabled = !Application.isPlaying;
        if (GUILayout.Button("Restore Poses from Prefs"))
            RestoreFromPrefs(ctrl);
        GUI.enabled = true;
    }

    // ------------------------------------------------------------------ //
    //  Auto-wire helpers
    // ------------------------------------------------------------------ //

    void AutoWireJoints(IiwaArmPosePresetController ctrl)
    {
        Undo.RecordObject(ctrl, "Auto-Wire Arm Joints");

        if (ctrl.armJoints == null || ctrl.armJoints.Length != 7)
            ctrl.armJoints = new ArticulationBody[7];

        // Search from root of the scene — joints are named iiwa_joint_1 … iiwa_joint_7
        // or iiwa_link_1 depending on URDF. Check both patterns.
        var allBodies = FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None);
        int found = 0;

        for (int i = 1; i <= 7; i++)
        {
            string nameA = $"iiwa_joint_{i}";
            string nameB = $"iiwa_link_{i}";

            ArticulationBody match = null;
            foreach (var b in allBodies)
            {
                if (b.name == nameA || b.name == nameB)
                {
                    match = b;
                    break;
                }
            }

            ctrl.armJoints[i - 1] = match;
            if (match != null) found++;
            else Debug.LogWarning($"[IiwaPreset] Could not find joint at index {i} " +
                                  $"(looked for '{nameA}' or '{nameB}')");
        }

        EditorUtility.SetDirty(ctrl);
        Debug.Log($"[IiwaPreset] Auto-wired {found}/7 arm joints.");
    }

    // ------------------------------------------------------------------ //
    //  Preview
    // ------------------------------------------------------------------ //

    static void DrawPosePreview(string label, float[] pose)
    {
        if (pose == null || pose.Length != 7)
        {
            EditorGUILayout.LabelField(label, "—  not captured");
            return;
        }

        bool allZero = true;
        foreach (var v in pose) if (v != 0f) { allZero = false; break; }

        string vals = allZero
            ? "not captured yet"
            : $"{pose[0]:F1}° {pose[1]:F1}° {pose[2]:F1}° {pose[3]:F1}° {pose[4]:F1}° {pose[5]:F1}° {pose[6]:F1}°";

        EditorGUILayout.LabelField(label, vals);
    }

    // ------------------------------------------------------------------ //
    //  EditorPrefs save / restore
    // ------------------------------------------------------------------ //

    static string PrefsKey(IiwaArmPosePresetController ctrl, string slot, int idx)
        => $"{PrefKey}{ctrl.gameObject.scene.name}_{ctrl.gameObject.name}_{slot}_{idx}";

    static void SaveToPrefs(IiwaArmPosePresetController ctrl)
    {
        SaveSlot(ctrl, "home", ctrl.homePoseDeg);
        SaveSlot(ctrl, "pregrasp", ctrl.preGraspPoseDeg);
        SaveSlot(ctrl, "lift", ctrl.liftPoseDeg);
        SaveSlot(ctrl, "place", ctrl.placePoseDeg);
        Debug.Log("[IiwaPreset] Poses saved to EditorPrefs.");
    }

    static void SaveSlot(IiwaArmPosePresetController ctrl, string slot, float[] values)
    {
        if (values == null) return;
        for (int i = 0; i < values.Length; i++)
            EditorPrefs.SetFloat(PrefsKey(ctrl, slot, i), values[i]);
    }

    static void RestoreFromPrefs(IiwaArmPosePresetController ctrl)
    {
        bool anyFound = RestoreSlot(ctrl, "home", ctrl.homePoseDeg);
        anyFound |= RestoreSlot(ctrl, "pregrasp", ctrl.preGraspPoseDeg);
        anyFound |= RestoreSlot(ctrl, "lift", ctrl.liftPoseDeg);
        anyFound |= RestoreSlot(ctrl, "place", ctrl.placePoseDeg);

        if (anyFound)
        {
            Undo.RecordObject(ctrl, "Restore Arm Poses from Prefs");
            EditorUtility.SetDirty(ctrl);
            Debug.Log("[IiwaPreset] Poses restored from EditorPrefs.");
        }
    }

    static bool RestoreSlot(IiwaArmPosePresetController ctrl, string slot, float[] target)
    {
        if (target == null || target.Length != 7) return false;

        bool found = false;
        for (int i = 0; i < 7; i++)
        {
            string key = PrefsKey(ctrl, slot, i);
            if (EditorPrefs.HasKey(key))
            {
                target[i] = EditorPrefs.GetFloat(key);
                found = true;
            }
        }
        return found;
    }
}
