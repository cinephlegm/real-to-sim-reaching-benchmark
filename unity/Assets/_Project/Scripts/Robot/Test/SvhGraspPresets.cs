using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Dev tool — hotkeys 1/2/3 snap the hand to stored grasp presets.
/// Tune the Inspector values after finding good synergy positions in
/// SvhHandSynergyKeyboardTester, then bake them in here.
///
/// Keys:
///   1  = OpenRest      (relaxed, fingers open, ready to approach)
///   2  = CubePreGrasp  (thumb lined up, fingers opened to cube width)
///   3  = CubeClose     (cube claw — strong thumb oppose, fingers curled)
/// </summary>
[RequireComponent(typeof(SvhSynergyHandDriver))]
public class SvhGraspPresets : MonoBehaviour
{
    [Header("1 — OpenRest")]
    public float openRestHandClose = 0.05f;
    public float openRestThumbOppose = 0.15f;
    public float openRestFingerSpread = 0.05f;

    [Header("2 — CubePreGrasp")]
    public float preGraspHandClose = 0.25f;
    public float preGraspThumbOppose = 0.55f;
    public float preGraspFingerSpread = 0.10f;

    [Header("3 — CubeClose  ★ CONFIRMED WORKING — lifted 28cm, 2 pads, speed<0.003)")]
    public float cubeCloseHandClose = 0.55f;
    public float cubeCloseThumbOppose = 0.75f;
    public float cubeCloseFingerSpread = 0.08f;

    SvhSynergyHandDriver _driver;
    string _activePreset = "none";

    void Awake()
    {
        _driver = GetComponent<SvhSynergyHandDriver>();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) ApplyOpenRest();
        if (kb.digit2Key.wasPressedThisFrame) ApplyCubePreGrasp();
        if (kb.digit3Key.wasPressedThisFrame) ApplyCubeClose();
    }

    public void ApplyOpenRest()
    {
        _driver.handClose = openRestHandClose;
        _driver.thumbOppose = openRestThumbOppose;
        _driver.fingerSpread = openRestFingerSpread;
        _driver.Apply();
        _activePreset = "1 — OpenRest";
    }

    public void ApplyCubePreGrasp()
    {
        _driver.handClose = preGraspHandClose;
        _driver.thumbOppose = preGraspThumbOppose;
        _driver.fingerSpread = preGraspFingerSpread;
        _driver.Apply();
        _activePreset = "2 — CubePreGrasp";
    }

    public void ApplyCubeClose()
    {
        _driver.handClose = cubeCloseHandClose;
        _driver.thumbOppose = cubeCloseThumbOppose;
        _driver.fingerSpread = cubeCloseFingerSpread;
        _driver.Apply();
        _activePreset = "3 — CubeClose";
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 57, 420, 42), GUI.skin.box);
        GUILayout.Label($"Preset active: {_activePreset}");
        GUILayout.Label("1=OpenRest  2=PreGrasp  3=CubeClose");
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
