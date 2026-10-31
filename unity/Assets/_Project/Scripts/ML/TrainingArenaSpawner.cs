using Unity.MLAgents;
using UnityEngine;

/// <summary>
/// Spawns N parallel training arenas when ML-Agents training is active (mlagents-learn connected).
/// Does nothing during demo recording or solo editor playtest — the single scene arena stays as-is.
///
/// Setup (one-time):
///   1. Run ML-Agents > Create Training Arena Prefab from the menu.
///   2. Add this component to any persistent scene object (e.g. the -----MANAGERS------ GO).
///   3. Assign the generated TrainingArena prefab to the arenaPrefab field.
///   4. Launch mlagents-learn, then press Play — arenas spawn automatically.
/// </summary>
public class TrainingArenaSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("The TrainingArena prefab created by ML-Agents > Create Training Arena Prefab.")]
    public GameObject arenaPrefab;

    [Header("Layout")]
    [Tooltip("How many parallel arenas to run during training. 4 is a good default for CPU.")]
    public int arenaCount = 4;

    [Tooltip("World-space X separation between arena centres (metres). 5 m keeps physics isolated.")]
    public float arenaSpacing = 5f;

    void Start()
    {
        Debug.Log($"[TrainingArenaSpawner] Start() — IsCommunicatorOn:{Academy.Instance.IsCommunicatorOn}  arenaPrefab:{(arenaPrefab == null ? "NULL" : arenaPrefab.name)}  arenaCount:{arenaCount}");

        // IsCommunicatorOn is true only when mlagents-learn is connected on port 5004.
        // False during solo playtest and demo recording → leave the single scene arena alone.
        if (!Academy.Instance.IsCommunicatorOn)
        {
            Debug.Log("[TrainingArenaSpawner] Communicator is OFF — solo play mode, no arenas spawned.");
            return;
        }

        if (arenaPrefab == null)
        {
            Debug.LogError("[TrainingArenaSpawner] arenaPrefab is not assigned. " +
                           "Run ML-Agents > Create Training Arena Prefab first.");
            return;
        }

        // Remove the single scene arena that was used for demo recording / editor testing.
        var sceneArena = GameObject.Find("TrainingArena");
        if (sceneArena != null)
        {
            Debug.Log($"[TrainingArenaSpawner] Destroying scene arena: '{sceneArena.name}'");
            Destroy(sceneArena);
        }
        else
            Debug.Log("[TrainingArenaSpawner] No scene arena 'TrainingArena' found to destroy.");

        // Spawn arenaCount copies side-by-side along the X axis.
        for (int i = 0; i < arenaCount; i++)
        {
            var pos = new Vector3(i * arenaSpacing, 0f, 0f);
            var spawned = Instantiate(arenaPrefab, pos, Quaternion.identity);
            Debug.Log($"[TrainingArenaSpawner] Spawned arena #{i}: '{spawned.name}' at {pos}");
        }

        Debug.Log($"[TrainingArenaSpawner] Spawned {arenaCount} training arenas " +
                  $"at {arenaSpacing} m spacing.");
    }
}
