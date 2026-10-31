using UnityEngine;
using UnityEngine.InputSystem;

public class GraspTestStageController : MonoBehaviour
{
    public GraspValidator validator;
    public EpisodeResetter resetter;

    public Key validateKey = Key.V;
    public Key resetKey = Key.Backspace;
    public Key toggleAutoEvaluateKey = Key.B;

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (validator != null && Keyboard.current[validateKey].wasPressedThisFrame)
            validator.ValidateNow();

        if (resetter != null && Keyboard.current[resetKey].wasPressedThisFrame)
            resetter.ResetEpisode();

        if (validator != null && Keyboard.current[toggleAutoEvaluateKey].wasPressedThisFrame)
            validator.autoEvaluate = !validator.autoEvaluate;
    }

    // HUD merged into DexterousPickPlaceAgent unified panel.
    public bool showHUD = false;

    void OnGUI()
    {
        if (!showHUD || validator == null)
            return;

        string status = validator.IsValidated ? "GRASP ✓" : $"NO  {validator.StableTimer:F2}/{validator.stableTimeRequired:F2}  {validator.LastFailureReason}";
        GUILayout.BeginArea(new Rect(10, 151, 420, 42), GUI.skin.box);
        GUILayout.Label(status);
        GUILayout.Label("[V] validate  [B] auto  [Bksp] reset episode");
        GUILayout.EndArea();
    }
}
