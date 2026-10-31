using UnityEngine;

/// <summary>
/// Attach to a trigger collider on the floor (or low table edge).
/// When the cube enters it, notifies the agent to flag dropped_floor=1 and end the episode.
/// </summary>
public class CubeFloorTrigger : MonoBehaviour
{
    [SerializeField] DexterousPickPlaceAgent agent;
    [SerializeField] string cubeTag = "GraspCube";

    void OnTriggerEnter(Collider other)
    {
        if (agent == null) return;
        if (!other.CompareTag(cubeTag)) return;
        agent.NotifyDroppedToFloor();
    }
}
