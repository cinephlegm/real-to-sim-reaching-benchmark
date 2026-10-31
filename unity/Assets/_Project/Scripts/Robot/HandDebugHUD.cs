using UnityEngine;

/// <summary>
/// Displays live MediaPipe UDP finger-flex values on screen.
/// Drop this component on any active GameObject in the scene (e.g. AgentRoot).
/// The overlay is only visible in Play mode.
///
/// Hotkey: Press  F9  to toggle the overlay on/off during Play.
/// </summary>
public class HandDebugHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The UdpSynergyReceiver to read from (auto-found if blank).")]
    public UdpSynergyReceiver receiver;

    [Header("Display")]
    public bool showHUD = true;
    public int fontSize = 18;
    public KeyCode toggleKey = KeyCode.F9;

    // Screen position
    const float kX = 10, kY = 10, kW = 300, kH = 260;

    GUIStyle _boxStyle, _labelStyle;

    void Start()
    {
        if (receiver == null)
            receiver = FindObjectOfType<UdpSynergyReceiver>();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            showHUD = !showHUD;
    }

    void OnGUI()
    {
        if (!showHUD) return;
        EnsureStyles();

        var r = new Rect(kX, kY, kW, kH);
        GUI.Box(r, GUIContent.none, _boxStyle);

        string status = receiver == null ? "NO RECEIVER" :
                        receiver.HasFreshData ? "<color=#00ff88>LIVE</color>" :
                                               "<color=#ff4444>STALE</color>";

        int pkts = receiver != null ? receiver.PacketsReceived : 0;
        float thumb = receiver != null ? receiver.ThumbFlex : 0f;
        float index = receiver != null ? receiver.IndexFlex : 0f;
        float middle = receiver != null ? receiver.MiddleFlex : 0f;
        float ring = receiver != null ? receiver.RingFlex : 0f;
        float pinky = receiver != null ? receiver.PinkyFlex : 0f;
        float spread = receiver != null ? receiver.FingerSpread : 0f;
        float roll = receiver != null ? receiver.WristRoll : 0f;

        float lx = kX + 6, ly = kY + 4, lh = fontSize + 4;

        GUI.Label(new Rect(lx, ly, kW - 12, lh * 1.5f),
                  $"<b>MediaPipe UDP</b>  {status}  pkts:{pkts}", _labelStyle);
        ly += lh + 2;

        DrawBar(lx, ly, "Thumb  ", thumb, _thumbColor); ly += lh;
        DrawBar(lx, ly, "Index  ", index, _fingerColor); ly += lh;
        DrawBar(lx, ly, "Middle ", middle, _fingerColor); ly += lh;
        DrawBar(lx, ly, "Ring   ", ring, _fingerColor); ly += lh;
        DrawBar(lx, ly, "Pinky  ", pinky, _fingerColor); ly += lh;
        DrawBar(lx, ly, "Spread ", spread, _spreadColor); ly += lh;
        DrawBar(lx, ly, "WristRl", roll, _rollColor);
    }

    // ── Helpers ───────────────────────────────────────────────────────────── //

    static readonly Color _thumbColor = new Color(1.0f, 0.5f, 0.2f);
    static readonly Color _fingerColor = new Color(0.3f, 0.8f, 1.0f);
    static readonly Color _spreadColor = new Color(0.8f, 1.0f, 0.3f);
    static readonly Color _rollColor = new Color(1.0f, 0.8f, 0.3f);

    void DrawBar(float x, float y, string label, float v, Color barCol)
    {
        // label
        GUI.Label(new Rect(x, y, 64, fontSize + 2), label, _labelStyle);

        // background
        float bx = x + 70, bw = 150, bh = fontSize;
        var bg = new Rect(bx, y + 2, bw, bh);
        var oldCol = GUI.color;
        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        GUI.DrawTexture(bg, Texture2D.whiteTexture);

        // bar fill
        GUI.color = barCol;
        GUI.DrawTexture(new Rect(bx, y + 2, bw * Mathf.Clamp01(v), bh), Texture2D.whiteTexture);
        GUI.color = oldCol;

        // numeric
        GUI.Label(new Rect(bx + bw + 4, y, 48, fontSize + 2), v.ToString("F2"), _labelStyle);
    }

    void EnsureStyles()
    {
        if (_labelStyle != null) return;

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTex(new Color(0f, 0f, 0f, 0.65f)) }
        };
        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            richText = true,
            normal = { textColor = Color.white }
        };
    }

    static Texture2D MakeTex(Color col)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, col);
        t.Apply();
        return t;
    }
}
