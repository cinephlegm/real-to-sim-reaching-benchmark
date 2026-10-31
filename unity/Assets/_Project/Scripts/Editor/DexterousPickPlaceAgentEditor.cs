using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DexterousPickPlaceAgent))]
public class DexterousPickPlaceAgentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Auto-Wire Scene References", GUILayout.Height(30)))
            AutoWire((DexterousPickPlaceAgent)target);
    }

    static void AutoWire(DexterousPickPlaceAgent agent)
    {
        Undo.RecordObject(agent, "Auto-Wire DexterousPickPlaceAgent");

        // Arm joints: iiwa_link_1 … iiwa_link_7
        for (int i = 0; i < 7; i++)
        {
            string linkName = $"iiwa_link_{i + 1}";
            GameObject go = GameObject.Find(linkName);
            if (go != null)
                agent.armJoints[i] = go.GetComponent<ArticulationBody>();
            else
                Debug.LogWarning($"[AutoWire] Could not find '{linkName}'");
        }

        // Palm attach point
        GameObject palm = GameObject.Find("PalmTargetMarker");
        if (palm != null)
            agent.palmAttachPoint = palm.transform;
        else
            Debug.LogWarning("[AutoWire] Could not find 'PalmTargetMarker'");

        // Arm preset controller
        agent.armPreset = Object.FindFirstObjectByType<IiwaArmPosePresetController>();
        if (agent.armPreset == null)
            Debug.LogWarning("[AutoWire] Could not find IiwaArmPosePresetController in scene");

        // SvhSynergyHandDriver — search whole scene
        agent.handDriver = Object.FindFirstObjectByType<SvhSynergyHandDriver>();
        if (agent.handDriver == null)
            Debug.LogWarning("[AutoWire] Could not find SvhSynergyHandDriver in scene");

        // UdpSynergyReceiver — on this same GameObject
        agent.udpReceiver = agent.GetComponent<UdpSynergyReceiver>();
        if (agent.udpReceiver == null)
            Debug.LogWarning("[AutoWire] UdpSynergyReceiver not found on this GameObject — add it first");

        // GraspValidator
        agent.validator = Object.FindFirstObjectByType<GraspValidator>();
        if (agent.validator == null)
            Debug.LogWarning("[AutoWire] Could not find GraspValidator in scene");

        // HybridHoldController (optional)
        agent.hold = Object.FindFirstObjectByType<HybridHoldController>();

        // EpisodeResetter
        agent.resetter = Object.FindFirstObjectByType<EpisodeResetter>();
        if (agent.resetter == null)
            Debug.LogWarning("[AutoWire] Could not find EpisodeResetter in scene");

        // MetricsReporter
        agent.metrics = Object.FindFirstObjectByType<MetricsReporter>();
        if (agent.metrics == null)
            Debug.LogWarning("[AutoWire] Could not find MetricsReporter in scene");

        // Cube — GameObject named CubeTarget with a Rigidbody
        GameObject cubeGo = GameObject.Find("CubeTarget");
        if (cubeGo != null)
            agent.cube = cubeGo.GetComponent<Rigidbody>();
        else
            Debug.LogWarning("[AutoWire] Could not find 'CubeTarget'");

        // Place marker
        GameObject placeGo = GameObject.Find("PlaceMarker");
        if (placeGo != null)
            agent.placeMarker = placeGo.transform;
        else
            Debug.LogWarning("[AutoWire] Could not find 'PlaceMarker'");

        EditorUtility.SetDirty(agent);
        Debug.Log("[AutoWire] Done. Check Console for any warnings.");
    }
}
