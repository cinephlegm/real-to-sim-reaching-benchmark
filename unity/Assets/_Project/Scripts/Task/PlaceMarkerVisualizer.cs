using UnityEngine;

/// <summary>
/// Attach to the PlaceMarker GameObject to visualise the success zone in the Scene and Game views.
/// Set radius to match DexterousPickPlaceAgent.placeRadius.
/// </summary>
public class PlaceMarkerVisualizer : MonoBehaviour
{
    [Tooltip("Must match DexterousPickPlaceAgent.placeRadius")]
    public float radius = 0.12f;

    [ColorUsage(false, false)]
    public Color gizmoColor = new Color(0f, 1f, 0.3f, 0.25f);

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.15f);
        Gizmos.DrawSphere(transform.position, radius);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
