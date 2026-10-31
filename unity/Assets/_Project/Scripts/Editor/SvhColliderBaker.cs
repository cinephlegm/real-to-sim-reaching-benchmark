using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SvhColliderBaker
{
    const string kRootName = "iiwa7_svh";
    const string kPalmName = "svh_base_link";
    const string kBoxName = "_PhysicsBox";
    const string kMatPath = "Assets/_Project/Physics/HandPad.physicMaterial";

    static readonly Vector3 kPalmSize = new Vector3(0.060f, 0.015f, 0.080f);
    static readonly Vector3 kPalmCenter = new Vector3(0f, 0.008f, 0.010f);

    [MenuItem("Tools/SVH/Bake Colliders Into Scene")]
    static void Bake()
    {
        var root = GameObject.Find(kRootName);
        if (root == null)
        {
            EditorUtility.DisplayDialog("SVH Collider Baker", $"Could not find '{kRootName}' in the open scene.", "OK");
            return;
        }

        var mat = GetOrCreateMaterial();
        var bodies = root.GetComponentsInChildren<ArticulationBody>(true);
        int added = 0;
        int removed = 0;
        int skipped = 0;

        foreach (var body in bodies)
        {
            if (!body.gameObject.name.StartsWith("svh_"))
                continue;

            var existing = body.transform.Find(kBoxName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                removed++;
            }

            foreach (var old in body.GetComponents<BoxCollider>())
            {
                if (!old.isTrigger)
                {
                    Undo.DestroyObjectImmediate(old);
                    removed++;
                }
            }

            if (!TryCalculateCollider(body, out Vector3 center, out Vector3 size))
            {
                skipped++;
                continue;
            }

            var boxGo = new GameObject(kBoxName);
            Undo.RegisterCreatedObjectUndo(boxGo, "Bake SVH Collider");
            boxGo.transform.SetParent(body.transform, false);
            boxGo.transform.localPosition = Vector3.zero;
            boxGo.transform.localRotation = Quaternion.identity;
            boxGo.transform.localScale = Vector3.one;
            boxGo.layer = body.gameObject.layer;

            var collider = boxGo.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = SanitizeSize(size);
            collider.isTrigger = false;
            collider.enabled = true;
            collider.sharedMaterial = mat;

            added++;
            Debug.Log($"[SVH Bake] {body.gameObject.name}: layer={LayerMask.LayerToName(boxGo.layer)} center={center:F4} size={collider.size:F4}");
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorUtility.SetDirty(root);

        EditorUtility.DisplayDialog(
            "SVH Collider Baker",
            $"Bake complete.\n\nRemoved: {removed}\nAdded: {added}\nSkipped non-physical/no-renderer bodies: {skipped}\n\nPress Ctrl+S to save the scene, then enter Play mode. Runtime must report the baked collider count.",
            "OK");
    }

    [MenuItem("Tools/SVH/Bake Colliders Into Scene", true)]
    static bool BakeValidate() => !Application.isPlaying;

    [MenuItem("Tools/SVH/Validate Baked Colliders")]
    static void ValidateBakedColliders()
    {
        var root = GameObject.Find(kRootName);
        if (root == null)
        {
            EditorUtility.DisplayDialog("SVH Collider Validator", $"Could not find '{kRootName}' in the open scene.", "OK");
            return;
        }

        var bodies = root.GetComponentsInChildren<ArticulationBody>(true);
        int svhBodies = 0;
        int physicalBodies = 0;
        int skipped = 0;
        int boxes = 0;
        int bad = 0;
        var report = new StringBuilder();

        foreach (var body in bodies)
        {
            if (!body.gameObject.name.StartsWith("svh_"))
                continue;

            svhBodies++;
            if (!ShouldHaveCollider(body))
            {
                skipped++;
                continue;
            }

            physicalBodies++;
            var box = body.transform.Find(kBoxName);
            if (box == null)
            {
                bad++;
                report.AppendLine($"Missing {kBoxName}: {body.gameObject.name}");
                continue;
            }

            var collider = box.GetComponent<BoxCollider>();
            if (collider == null)
            {
                bad++;
                report.AppendLine($"Missing BoxCollider: {body.gameObject.name}/{kBoxName}");
                continue;
            }

            boxes++;
            if (!collider.enabled || collider.isTrigger || collider.size.x <= 0f || collider.size.y <= 0f || collider.size.z <= 0f)
            {
                bad++;
                report.AppendLine($"Invalid collider: {body.gameObject.name}/{kBoxName}");
            }
        }

        string status = bad == 0 ? "OK" : "PROBLEMS FOUND";
        string msg = $"{status}\n\nSVH ArticulationBodies: {svhBodies}\nPhysical SVH bodies expected to collide: {physicalBodies}\nSkipped virtual/no-renderer bodies: {skipped}\nBaked _PhysicsBox colliders: {boxes}\nProblems: {bad}\n\n{report}";
        Debug.Log($"[SVH Validate] {msg}");
        EditorUtility.DisplayDialog("SVH Collider Validator", msg, "OK");
    }

    [MenuItem("Tools/SVH/Validate Baked Colliders", true)]
    static bool ValidateBakedCollidersValidate() => !Application.isPlaying;

    static bool ShouldHaveCollider(ArticulationBody body)
    {
        if (body.gameObject.name == kPalmName)
            return true;

        foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.GetComponentInParent<ArticulationBody>() == body)
                return true;
        }

        return false;
    }

    static bool TryCalculateCollider(ArticulationBody body, out Vector3 center, out Vector3 size)
    {
        center = Vector3.zero;
        size = Vector3.zero;

        Renderer best = null;
        foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.GetComponentInParent<ArticulationBody>() != body)
                continue;

            if (best == null || renderer.bounds.size.sqrMagnitude > best.bounds.size.sqrMagnitude)
                best = renderer;
        }

        if (best == null)
        {
            if (body.gameObject.name != kPalmName)
                return false;

            center = kPalmCenter;
            size = kPalmSize;
            return true;
        }

        Bounds worldBounds = best.bounds;
        Vector3 min = Vector3.one * float.MaxValue;
        Vector3 max = Vector3.one * -float.MaxValue;
        Vector3 wMin = worldBounds.min;
        Vector3 wMax = worldBounds.max;

        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                {
                    var corner = new Vector3(x == 0 ? wMin.x : wMax.x, y == 0 ? wMin.y : wMax.y, z == 0 ? wMin.z : wMax.z);
                    var local = body.transform.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }

        center = (min + max) * 0.5f;
        size = max - min;
        return true;
    }

    static Vector3 SanitizeSize(Vector3 size)
    {
        return new Vector3(
            Mathf.Max(Mathf.Abs(size.x), 0.006f),
            Mathf.Max(Mathf.Abs(size.y), 0.006f),
            Mathf.Max(Mathf.Abs(size.z), 0.006f));
    }

    static PhysicMaterial GetOrCreateMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(kMatPath);
        if (mat != null)
            return mat;

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Physics"))
            AssetDatabase.CreateFolder("Assets/_Project", "Physics");

        mat = new PhysicMaterial("HandPad")
        {
            staticFriction = 1.2f,
            dynamicFriction = 0.9f,
            frictionCombine = PhysicMaterialCombine.Maximum,
            bounciness = 0f,
            bounceCombine = PhysicMaterialCombine.Minimum
        };

        AssetDatabase.CreateAsset(mat, kMatPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
