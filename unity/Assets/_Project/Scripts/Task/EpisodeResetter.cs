using UnityEngine;

public class EpisodeResetter : MonoBehaviour
{
    public Rigidbody cube;
    public Transform cubeSpawn;

    public SvhSynergyHandDriver hand;
    public GraspValidator validator;
    public CubeContactMonitor monitor;
    public HybridHoldController hold;

    [Header("Stress Testing (optional)")]
    [Tooltip("Assign the StressTestController to enable perturbation testing. Leave null for normal training.")]
    public StressTestController stressTest;

    public float openRestClose = 0.05f;
    public float openRestThumbOppose = 0.15f;
    public float openRestFingerSpread = 0.10f;

    [Header("Cube reset physics")]
    public bool forceDynamicCube = true;
    public bool rebuildCubeContactsAfterReset = true;
    public CollisionDetectionMode cubeCollisionMode = CollisionDetectionMode.ContinuousDynamic;

    public void ResetEpisode()
    {
        // Apply stress-test spawn offset BEFORE the cube position is written
        stressTest?.PreReset();

        Debug.Log($"[EpisodeResetter:{name}] ResetEpisode() START — hold.attached:{hold?.attached}  cube:{cube != null}  cubeSpawn:{cubeSpawn != null}");

        if (hold != null && hold.attached)
        {
            Debug.Log($"[EpisodeResetter:{name}] Detaching hold...");
            hold.Detach();
        }

        if (cube != null && cubeSpawn != null)
        {
            Debug.Log($"[EpisodeResetter:{name}] Resetting cube — cubeParent:{cube.transform.parent?.name ?? "none"}  forceDynamic:{forceDynamicCube}");

            if (cube.transform.parent != null)
                cube.transform.SetParent(null, true);

            if (forceDynamicCube)
            {
                cube.isKinematic = false;
                cube.useGravity = true;
                cube.detectCollisions = true;
            }

            cube.collisionDetectionMode = cubeCollisionMode;
            cube.sleepThreshold = 0f;
            cube.velocity = Vector3.zero;
            cube.angularVelocity = Vector3.zero;
            cube.position = cubeSpawn.position;
            cube.rotation = cubeSpawn.rotation;

            Debug.Log($"[EpisodeResetter:{name}] Calling Physics.SyncTransforms()...");
            Physics.SyncTransforms();
            cube.WakeUp();
            Debug.Log($"[EpisodeResetter:{name}] Cube reset done — pos:{cube.position}");
        }
        else
            Debug.LogWarning($"[EpisodeResetter:{name}] Cube or cubeSpawn is NULL! cube:{cube != null} cubeSpawn:{cubeSpawn != null}");

        if (hand != null)
        {
            Debug.Log($"[EpisodeResetter:{name}] Resetting hand to open rest pose.");
            hand.SetSynergies(openRestClose, openRestThumbOppose, openRestFingerSpread);
            // Physically teleport hand joints to the new targets — SetSynergies only
            // sets xDrive.target; without this, fingers PD-chase the target over many frames.
            hand.TeleportToCurrentTargets();
        }
        else
            Debug.LogWarning($"[EpisodeResetter:{name}] hand is NULL — hand not reset.");

        if (validator != null)
            validator.ClearValidation();
        else
            Debug.LogWarning($"[EpisodeResetter:{name}] validator is NULL.");

        if (monitor != null)
            monitor.ApplyCubePhysicsSettings(false);

        Debug.Log($"[EpisodeResetter:{name}] ResetEpisode() DONE.");
    }
}
