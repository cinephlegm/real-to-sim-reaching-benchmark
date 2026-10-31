using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Optional stabilised-hold fallback for manual testing only.
/// NOT used during training or evaluation — the dissertation claim is pure physics grasping.
/// Keep autoAttachWhenValidated = false at all times.
/// Use C/X manually only when debugging lift/place trajectories in isolation.
/// </summary>
public class HybridHoldController : MonoBehaviour
{
    public GraspValidator validator;
    public Rigidbody cube;
    public Transform attachPoint;

    public bool autoAttachWhenValidated = false;
    public bool attached;

    public Key attachKey = Key.C;
    public Key detachKey = Key.X;

    Vector3 cachedLocalPosition;
    Quaternion cachedLocalRotation;

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (!attached && autoAttachWhenValidated && validator != null && validator.IsValidated)
            Attach();

        if (Keyboard.current[attachKey].wasPressedThisFrame)
            Attach();

        if (Keyboard.current[detachKey].wasPressedThisFrame)
            Detach();
    }

    public void Attach()
    {
        if (attached || cube == null || attachPoint == null)
            return;

        cachedLocalPosition = attachPoint.InverseTransformPoint(cube.worldCenterOfMass);
        cachedLocalRotation = Quaternion.Inverse(attachPoint.rotation) * cube.rotation;

        cube.velocity = Vector3.zero;
        cube.angularVelocity = Vector3.zero;
        cube.isKinematic = true;
        cube.useGravity = false;
        cube.transform.SetParent(attachPoint, true);

        attached = true;
    }

    public void Detach()
    {
        if (!attached || cube == null)
            return;

        cube.transform.SetParent(null, true);
        cube.isKinematic = false;
        cube.useGravity = true;

        attached = false;
    }

    public void SnapHeldCubeToAttachPoint()
    {
        if (!attached || cube == null || attachPoint == null)
            return;

        cube.transform.position = attachPoint.TransformPoint(cachedLocalPosition);
        cube.transform.rotation = attachPoint.rotation * cachedLocalRotation;
    }

    void LateUpdate()
    {
        if (attached)
            SnapHeldCubeToAttachPoint();
    }

    // OnGUI removed — use Inspector to monitor hold state during recording.
}
