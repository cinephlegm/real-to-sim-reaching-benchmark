using UnityEngine;
using UnityEngine.InputSystem;

public class SvhHandSynergyKeyboardTester : MonoBehaviour
{
    public SvhSynergyHandDriver hand;
    public float speed = 0.8f;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float dt = Time.deltaTime;

        if (kb.rKey.isPressed)
            hand.handClose = Mathf.Clamp01(hand.handClose + speed * dt);

        if (kb.fKey.isPressed)
            hand.handClose = Mathf.Clamp01(hand.handClose - speed * dt);

        if (kb.tKey.isPressed)
            hand.thumbOppose = Mathf.Clamp01(hand.thumbOppose + speed * dt);

        if (kb.gKey.isPressed)
            hand.thumbOppose = Mathf.Clamp01(hand.thumbOppose - speed * dt);

        if (kb.yKey.isPressed)
            hand.fingerSpread = Mathf.Clamp01(hand.fingerSpread + speed * dt);

        if (kb.hKey.isPressed)
            hand.fingerSpread = Mathf.Clamp01(hand.fingerSpread - speed * dt);

        if (kb.digit0Key.wasPressedThisFrame)
        {
            hand.handClose = 0f;
            hand.thumbOppose = 0f;
            hand.fingerSpread = 0f;
        }

        hand.Apply();
    }

    void OnGUI()
    {
        var rect = new Rect(10, 177, 420, 172);
        DrawSharpBox(rect);
        GUILayout.BeginArea(rect);
        GUILayout.Label($"handClose: {hand.handClose:F2}");
        GUILayout.Label($"thumbOppose: {hand.thumbOppose:F2}");
        GUILayout.Label($"fingerSpread: {hand.fingerSpread:F2}");
        GUILayout.Label("R/F = close open");
        GUILayout.Label("T/G = thumb oppose in/out");
        GUILayout.Label("Y/H = spread in/out");
        GUILayout.Label("0 = reset");
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
