#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor utility: adds a convex MeshCollider to every visual-mesh child
/// under SVH hand ArticulationBody links.
///
/// The SVH .dae files are imported with addColliders=0, so Unity generates
/// no physics geometry from them.  This script uses the existing visual mesh
/// (already at scale 0.001 so vertices are in metres in world space) as a
/// convex hull — a much more accurate shape than the BoxCollider fallbacks,
/// and convex MeshColliders are fully supported on ArticulationBody.
///
/// Run ONCE via:  Tools → Setup SVH Convex Mesh Colliders
/// Colliders are saved into the scene file and exist at runtime with zero
/// extra setup cost.
/// </summary>
public static class SvhConvexColliderSetup
{
    // DISABLED — use SvhHandColliderSetup (runtime BoxColliders) instead.
    // Convex MeshColliders from this tool hit the 256-poly PhysX limit (partial
    // hulls) and, if run during Play mode, add colliders AFTER IgnoreCollision
    // pairs are set, causing intra-hand self-collisions that jam the joint drives.
    [MenuItem("Tools/Setup SVH Convex Mesh Colliders [DISABLED]", true)]
    static bool RunValidate() => false; // always greyed out — do not use

    [MenuItem("Tools/Setup SVH Convex Mesh Colliders [DISABLED]")]
    static void Run()
    {
        int added = 0;
        int fixed_ = 0;
        int skipped = 0;

        var allABs = Object.FindObjectsOfType<ArticulationBody>(includeInactive: true);
        foreach (var ab in allABs)
        {
            if (!ab.gameObject.name.StartsWith("svh_")) continue;

            // The visual mesh lives at  <link>/Visuals/unnamed  (MeshFilter + MeshRenderer,
            // scale 0.001).  We add / fix the MeshCollider on that same GameObject so it
            // participates in the parent AB's compound collider at the correct world scale.
            var meshFilters = ab.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;

                var go = mf.gameObject;

                // Skip if a child ArticulationBody owns this MeshFilter — shouldn't happen
                // but guards against double-counting nested links.
                var ownAB = go.GetComponentInParent<ArticulationBody>();
                if (ownAB != ab) continue;

                var mc = go.GetComponent<MeshCollider>();
                if (mc == null)
                {
                    mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                    EditorUtility.SetDirty(go);
                    added++;
                }
                else if (!mc.convex)
                {
                    mc.convex = true;
                    EditorUtility.SetDirty(go);
                    fixed_++;
                }
                else
                {
                    skipped++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        string msg = $"Added: {added}   Fixed (made convex): {fixed_}   Already OK: {skipped}";
        Debug.Log($"[SvhConvexColliderSetup] {msg}");
        EditorUtility.DisplayDialog("SVH Convex Mesh Colliders", msg, "OK");
    }
}
#endif
