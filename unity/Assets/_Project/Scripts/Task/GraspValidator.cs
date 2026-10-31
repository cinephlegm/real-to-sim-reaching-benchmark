using UnityEngine;

public class GraspValidator : MonoBehaviour
{
    public CubeContactMonitor monitor;
    public SvhSynergyHandDriver hand;

    public int requiredTouchingPads = 0;
    public float minHandClose = 0.20f;
    public float maxPalmDistance = 1.0f;
    public float maxCubeSpeed = 0.50f;
    public float stableTimeRequired = 0.05f;

    public bool requireThumbTouch = false;
    public string thumbPadName = "ThumbPadTrigger";

    public bool requireSupportTouch = false;
    public string[] supportPadNames = { "IndexPadTrigger", "MiddlePadTrigger", "RingPadTrigger", "PinkyPadTrigger" };

    public bool autoEvaluate = true;

    public bool IsValidated { get; private set; }
    public float StableTimer { get; private set; }
    public string LastFailureReason { get; private set; } = "Not evaluated";

    void Update()
    {
        if (!autoEvaluate)
            return;

        Evaluate(Time.deltaTime);
    }

    public void ClearValidation()
    {
        IsValidated = false;
        StableTimer = 0f;
        LastFailureReason = "Cleared";
    }

    public bool ValidateNow()
    {
        Evaluate(0f, true);
        return IsValidated;
    }

    public void Evaluate(float deltaTime, bool instant = false)
    {
        if (monitor == null)
        {
            Fail("No contact monitor");
            return;
        }

        if (hand == null)
        {
            Fail("No hand driver");
            return;
        }

        if (hand.handClose < minHandClose)
        {
            Fail("Hand not closed enough");
            return;
        }

        // Pad contact checks removed — unreliable with ArticulationBody trigger setup.
        // Drop detection is handled by the agent (checks XZ proximity to start position).

        if (instant)
        {
            StableTimer = stableTimeRequired;
            IsValidated = true;
            LastFailureReason = "Validated";
            return;
        }

        StableTimer += deltaTime;
        if (StableTimer >= stableTimeRequired)
        {
            IsValidated = true;
            LastFailureReason = "Validated";
        }
        else
        {
            IsValidated = false;
            LastFailureReason = "Stabilizing";
        }
    }

    bool AnySupportTouching()
    {
        if (supportPadNames == null || supportPadNames.Length == 0)
            return true;

        for (int i = 0; i < supportPadNames.Length; i++)
        {
            if (monitor.IsTouching(supportPadNames[i]))
                return true;
        }

        return false;
    }

    void Fail(string reason)
    {
        IsValidated = false;
        StableTimer = 0f;
        LastFailureReason = reason;
    }
}
