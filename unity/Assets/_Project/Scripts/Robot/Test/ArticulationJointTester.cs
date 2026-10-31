using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ArticulationJointTester : MonoBehaviour
{
    public bool includeArm = true;
    public bool includeHand = true;
    public bool setRootImmovable = true;
    public bool disableGravity = true;

    public float armStiffness = 3000f;
    public float armDamping = 300f;
    public float armForceLimit = 300f;

    public float handStiffness = 400f;
    public float handDamping = 40f;
    public float handForceLimit = 80f;

    public float stepPerSecond = 30f;

    [Header("Highlight")]
    public Color highlightColor = new Color(1f, 0.4f, 0f, 1f); // orange
    public float highlightScale = 0.06f;

    List<ArticulationBody> joints = new();
    int selectedIndex;
    GameObject _highlightSphere;

    void Awake()
    {
        var bodies = GetComponentsInChildren<ArticulationBody>(true);

        foreach (var body in bodies)
        {
            if (disableGravity)
                body.useGravity = false;

            if (setRootImmovable && body.isRoot)
                body.immovable = true;
        }

        foreach (var body in bodies)
        {
            if (body.jointType != ArticulationJointType.RevoluteJoint)
                continue;

            bool isArm = body.name.StartsWith("iiwa_");
            bool isHand = body.name.StartsWith("svh_");

            if ((isArm && !includeArm) || (isHand && !includeHand))
                continue;

            joints.Add(body);

            var drive = body.xDrive;
            drive.stiffness = isArm ? armStiffness : handStiffness;
            drive.damping = isArm ? armDamping : handDamping;
            drive.forceLimit = isArm ? armForceLimit : handForceLimit;
            drive.targetVelocity = 0f;

            float currentDeg = 0f;
            if (body.jointPosition.dofCount > 0)
                currentDeg = body.jointPosition[0] * Mathf.Rad2Deg;

            drive.target = currentDeg;
            body.xDrive = drive;
        }

        selectedIndex = 0;
        CreateHighlight();
        MoveHighlight();
        PrintSelected();
    }

    void Update()
    {
        if (joints.Count == 0)
            return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.leftBracketKey.wasPressedThisFrame)
        {
            selectedIndex = Mathf.Max(0, selectedIndex - 1);
            MoveHighlight();
            PrintSelected();
        }

        if (kb.rightBracketKey.wasPressedThisFrame)
        {
            selectedIndex = Mathf.Min(joints.Count - 1, selectedIndex + 1);
            MoveHighlight();
            PrintSelected();
        }

        float delta = 0f;

        if (kb.qKey.isPressed)
            delta -= stepPerSecond * Time.deltaTime;

        if (kb.eKey.isPressed)
            delta += stepPerSecond * Time.deltaTime;

        if (Mathf.Abs(delta) > 0f)
            MoveSelected(delta);

        if (kb.zKey.wasPressedThisFrame)
            ResetSelectedToCurrent();

        if (kb.xKey.wasPressedThisFrame)
            ResetSelectedToZero();
    }

    void MoveSelected(float deltaDeg)
    {
        var joint = joints[selectedIndex];
        var drive = joint.xDrive;
        drive.target = Mathf.Clamp(drive.target + deltaDeg, drive.lowerLimit, drive.upperLimit);
        joint.xDrive = drive;
    }

    void ResetSelectedToCurrent()
    {
        var joint = joints[selectedIndex];
        var drive = joint.xDrive;

        float currentDeg = 0f;
        if (joint.jointPosition.dofCount > 0)
            currentDeg = joint.jointPosition[0] * Mathf.Rad2Deg;

        drive.target = Mathf.Clamp(currentDeg, drive.lowerLimit, drive.upperLimit);
        joint.xDrive = drive;
    }

    void ResetSelectedToZero()
    {
        var joint = joints[selectedIndex];
        var drive = joint.xDrive;
        drive.target = Mathf.Clamp(0f, drive.lowerLimit, drive.upperLimit);
        joint.xDrive = drive;
    }

    void CreateHighlight()
    {
        _highlightSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _highlightSphere.name = "_JointHighlight";
        _highlightSphere.transform.localScale = Vector3.one * highlightScale;

        // Remove the collider so it doesn't interfere with physics
        Destroy(_highlightSphere.GetComponent<Collider>());

        // Make it a bright emissive colour with no shadows
        var rend = _highlightSphere.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = highlightColor;
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", highlightColor * 2f);
        rend.material = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }

    void MoveHighlight()
    {
        if (_highlightSphere == null || joints.Count == 0) return;
        _highlightSphere.transform.SetParent(joints[selectedIndex].transform, false);
        _highlightSphere.transform.localPosition = Vector3.zero;
    }

    void PrintSelected()
    {
        if (joints.Count == 0)
            return;

        var joint = joints[selectedIndex];
        Debug.Log($"Selected joint [{selectedIndex + 1}/{joints.Count}] {joint.name}");
    }

    void OnGUI()
    {
        if (joints.Count == 0)
            return;

        var joint = joints[selectedIndex];
        var drive = joint.xDrive;

        var rect = new Rect(10, 10, 420, 162);
        DrawSharpBox(rect);
        GUILayout.BeginArea(rect);
        GUILayout.Label($"Selected: {joint.name} ({selectedIndex + 1}/{joints.Count})");
        GUILayout.Label($"Target: {drive.target:F2}   Limits: {drive.lowerLimit:F2} to {drive.upperLimit:F2}");
        GUILayout.Label("[ / ] select joint");
        GUILayout.Label("Q / E move joint");
        GUILayout.Label("Z snap target to current pose");
        GUILayout.Label("X set target to zero");
        GUILayout.EndArea();
    }

    static void DrawSharpBox(Rect r)
    {
        GUI.color = new Color(0.45f, 0.45f, 0.45f, 0.95f);
        GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), Texture2D.whiteTexture);
        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.88f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
