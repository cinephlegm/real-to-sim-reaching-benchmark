#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool: ML-Agents > Create Training Arena Prefab
/// Groups all arena objects under a TrainingArena root and saves to Assets/_Project/Prefabs/TrainingArena.prefab.
/// Run this ONCE from the scene that has your single arena set up (IIWA7_SVH_CUBE_GRASP_POSE_TEST).
/// After saving, add TrainingArenaSpawner to any persistent scene object and assign the prefab.
/// </summary>
public static class CreateTrainingArenaPrefab
{
    // Per-arena objects: each duplicate arena needs its own copy of these.
    // FLOOR is intentionally excluded — it is a single shared scene-wide ground plane.
    // Directional Light, Main Camera, and manager GOs are also excluded for the same reason.
    static readonly string[] ArenaObjectNames =
    {
        "TABLE-PICKUP",
        "TABLE-PLACE",
        "CubeTarget",
        "FloorTrigger",   // BoxCollider trigger — references this arena's agent
        "EpisodeManager", // EpisodeResetter — resets this arena
        "StatsLogger",    // DemoStatsLogger — logs this arena's episodes
        "AgentRoot",      // MetricsReporter — reports this arena's metrics
        "CubeSpawn",
        "PalmTargetMarker",
    };

    [MenuItem("ML-Agents/Create Training Arena Prefab")]
    static void Create()
    {
        var agent = Object.FindObjectOfType<DexterousPickPlaceAgent>();
        if (agent == null)
        {
            EditorUtility.DisplayDialog("Create Training Arena Prefab",
                "No DexterousPickPlaceAgent found in the open scene.\n" +
                "Open IIWA7_SVH_CUBE_GRASP_POSE_TEST and try again.", "OK");
            return;
        }

        // Check the robot root isn't already inside another TrainingArena
        if (agent.transform.parent != null && agent.transform.parent.name == "TrainingArena")
        {
            EditorUtility.DisplayDialog("Create Training Arena Prefab",
                "Robot is already parented under TrainingArena.\n" +
                "The prefab has already been created.", "OK");
            return;
        }

        var arenaRoot = new GameObject("TrainingArena");
        Undo.RegisterCreatedObjectUndo(arenaRoot, "Create TrainingArena");

        // Parent the robot root (the GO that carries DexterousPickPlaceAgent)
        Undo.SetTransformParent(agent.transform, arenaRoot.transform, "Parent robot to arena");

        // Parent all other named arena objects
        foreach (var objName in ArenaObjectNames)
        {
            var go = GameObject.Find(objName);
            if (go != null)
                Undo.SetTransformParent(go.transform, arenaRoot.transform, "Parent " + objName + " to arena");
            else
                Debug.LogWarning("[CreateTrainingArenaPrefab] GameObject not found: " + objName);
        }

        // Save prefab
        const string dir = "Assets/_Project/Prefabs";
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        const string prefabPath = dir + "/TrainingArena.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            arenaRoot, prefabPath, InteractionMode.UserAction);

        AssetDatabase.Refresh();
        Selection.activeObject = prefab;

        Debug.Log("[CreateTrainingArenaPrefab] Saved to " + prefabPath +
                  ". Now add TrainingArenaSpawner to a scene manager object and assign this prefab.");
    }
}
#endif
