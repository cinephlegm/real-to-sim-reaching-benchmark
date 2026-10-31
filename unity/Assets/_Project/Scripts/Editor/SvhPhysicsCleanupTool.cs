#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SvhPhysicsCleanupTool
{
    const string RootName = "iiwa7_svh";
    const string PhysicsBoxName = "_PhysicsBox";

    [MenuItem("Tools/SVH/Cleanup Dangerous Physics Colliders")]
    static void Cleanup()
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            EditorUtility.DisplayDialog("SVH Physics Cleanup", $"Could not find '{RootName}' in the open scene.", "OK");
            return;
        }

        int removedMeshColliders = 0;
        int disabledPhysicsBoxes = 0;
        int disabledDirectBoxes = 0;
        int removedGeneratedTriggers = 0;

        foreach (var meshCollider in root.GetComponentsInChildren<MeshCollider>(true))
        {
            if (!meshCollider.isTrigger)
            {
                Undo.DestroyObjectImmediate(meshCollider);
                removedMeshColliders++;
            }
        }

        foreach (var articulationBody in root.GetComponentsInChildren<ArticulationBody>(true))
        {
            if (!articulationBody.gameObject.name.StartsWith("svh_"))
                continue;

            Transform physicsBox = articulationBody.transform.Find(PhysicsBoxName);
            if (physicsBox != null)
            {
                foreach (var collider in physicsBox.GetComponentsInChildren<Collider>(true))
                {
                    if (collider.enabled)
                    {
                        collider.enabled = false;
                        EditorUtility.SetDirty(collider);
                        disabledPhysicsBoxes++;
                    }
                }
            }

            foreach (var box in articulationBody.GetComponents<BoxCollider>())
            {
                if (!box.isTrigger && box.enabled)
                {
                    box.enabled = false;
                    EditorUtility.SetDirty(box);
                    disabledDirectBoxes++;
                }
            }
        }

        string[] triggerNames =
        {
            "ThumbPadTrigger",
            "IndexPadTrigger",
            "MiddlePadTrigger",
            "RingPadTrigger",
            "PinkyPadTrigger"
        };

        foreach (string triggerName in triggerNames)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == triggerName)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                    removedGeneratedTriggers++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorUtility.SetDirty(root);

        string message =
            $"Cleanup complete.\n\n" +
            $"Removed non-trigger MeshColliders: {removedMeshColliders}\n" +
            $"Disabled baked _PhysicsBox colliders: {disabledPhysicsBoxes}\n" +
            $"Disabled direct solid BoxColliders: {disabledDirectBoxes}\n" +
            $"Removed generated fingertip triggers: {removedGeneratedTriggers}\n\n" +
            $"Press Ctrl+S, close Unity, reopen the project, then check whether the convex MeshCollider warnings are gone.";

        Debug.Log("[SVH Physics Cleanup] " + message);
        EditorUtility.DisplayDialog("SVH Physics Cleanup", message, "OK");
    }

    [MenuItem("Tools/SVH/Cleanup Dangerous Physics Colliders", true)]
    static bool CleanupValidate()
    {
        return !Application.isPlaying;
    }
}
#endif
