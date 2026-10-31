using UnityEngine;
using UnityEditor;

/// <summary>
/// Auto-wires SvhJointMap fields by searching for ArticulationBodies
/// whose GameObjects match the expected child-link names from the URDF.
///
/// Joint → Child Link mapping (from iiwa7_svh_right_hand_unity.urdf):
///   thumbOpposition  ← svh_Thumb_Opposition  → child link: svh_z
///   thumbFlexion     ← svh_Thumb_Flexion      → child link: svh_a
///   thumbJ3          ← svh_j3                 → child link: svh_b
///   thumbJ4          ← svh_j4                 → child link: svh_c
///   indexProximal    ← svh_Index_Finger_Proximal → child link: svh_l
///   indexDistal      ← svh_Index_Finger_Distal   → child link: svh_p
///   indexJ14         ← svh_j14               → child link: svh_t
///   middleProximal   ← svh_Middle_Finger_Proximal → child link: svh_k
///   middleDistal     ← svh_Middle_Finger_Distal   → child link: svh_o
///   middleJ15        ← svh_j15               → child link: svh_s
///   ringBase         ← svh_Ring_Finger        → child link: svh_j
///   ringJ12          ← svh_j12               → child link: svh_n
///   ringJ16          ← svh_j16               → child link: svh_r
///   pinkyBase        ← svh_Pinky             → child link: svh_i
///   pinkyJ13         ← svh_j13               → child link: svh_m
///   pinkyJ17         ← svh_j17               → child link: svh_q
///   fingerSpread     ← svh_Finger_Spread     → child link: svh_virtual_i
///   indexSpread      ← svh_index_spread      → child link: svh_virtual_l
///   ringSpread       ← svh_ring_spread       → child link: svh_virtual_j
/// </summary>
[CustomEditor(typeof(SvhJointMap))]
public class SvhJointMapAutoWirer : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8);
        if (GUILayout.Button("Auto-Wire Joints from Hierarchy"))
        {
            AutoWire((SvhJointMap)target);
        }
    }

    static void AutoWire(SvhJointMap map)
    {
        var root = map.transform;
        int wired = 0;
        int missing = 0;

        Undo.RecordObject(map, "Auto-Wire SvhJointMap");

        map.thumbOpposition = Resolve(root, "svh_z", ref wired, ref missing);
        map.thumbJ5 = Resolve(root, "svh_e2", ref wired, ref missing);
        map.thumbFlexion = Resolve(root, "svh_a", ref wired, ref missing);
        map.thumbJ3 = Resolve(root, "svh_b", ref wired, ref missing);
        map.thumbJ4 = Resolve(root, "svh_c", ref wired, ref missing);

        map.indexProximal = Resolve(root, "svh_l", ref wired, ref missing);
        map.indexDistal = Resolve(root, "svh_p", ref wired, ref missing);
        map.indexJ14 = Resolve(root, "svh_t", ref wired, ref missing);

        map.middleProximal = Resolve(root, "svh_k", ref wired, ref missing);
        map.middleDistal = Resolve(root, "svh_o", ref wired, ref missing);
        map.middleJ15 = Resolve(root, "svh_s", ref wired, ref missing);

        map.ringBase = Resolve(root, "svh_j", ref wired, ref missing);
        map.ringJ12 = Resolve(root, "svh_n", ref wired, ref missing);
        map.ringJ16 = Resolve(root, "svh_r", ref wired, ref missing);

        map.pinkyBase = Resolve(root, "svh_i", ref wired, ref missing);
        map.pinkyJ13 = Resolve(root, "svh_m", ref wired, ref missing);
        map.pinkyJ17 = Resolve(root, "svh_q", ref wired, ref missing);

        map.fingerSpread = Resolve(root, "svh_virtual_i", ref wired, ref missing);
        map.indexSpread = Resolve(root, "svh_virtual_l", ref wired, ref missing);
        map.ringSpread = Resolve(root, "svh_virtual_j", ref wired, ref missing);

        EditorUtility.SetDirty(map);

        if (missing == 0)
            Debug.Log($"[SvhJointMapAutoWirer] All {wired}/20 joints wired successfully.");
        else
            Debug.LogWarning($"[SvhJointMapAutoWirer] Wired {wired}/20. {missing} missing — check Console for details.");
    }

    static ArticulationBody Resolve(Transform root, string linkName,
        ref int wired, ref int missing)
    {
        var t = FindDeep(root, linkName);
        if (t == null)
        {
            Debug.LogWarning($"[SvhJointMapAutoWirer] Could not find link: '{linkName}'");
            missing++;
            return null;
        }

        var ab = t.GetComponent<ArticulationBody>();
        if (ab == null)
        {
            Debug.LogWarning($"[SvhJointMapAutoWirer] Found '{linkName}' but it has no ArticulationBody.");
            missing++;
            return null;
        }

        Debug.Log($"[SvhJointMapAutoWirer]   OK  {linkName}");
        wired++;
        return ab;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var result = FindDeep(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
