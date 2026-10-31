using UnityEngine;
using UnityEngine.InputSystem;

public class PhysicsGraspDebugger : MonoBehaviour
{
    public CubeContactMonitor monitor;
    public float liftHeight = 0.02f;

    Vector3 _startCubePosition;

    void Start()
    {
        if (monitor != null && monitor.cubeRigidbody != null)
            _startCubePosition = monitor.cubeRigidbody.position;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit9Key.wasPressedThisFrame && monitor != null && monitor.cubeRigidbody != null)
            _startCubePosition = monitor.cubeRigidbody.position;
    }

    // OnGUI removed — physics grasp info is shown in GraspTestStageController panel.

    static void DrawSharpBox(Rect r)
    {
        GUI.color = new Color(0.45f, 0.45f, 0.45f, 0.95f);
        GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), Texture2D.whiteTexture);
        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.88f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
