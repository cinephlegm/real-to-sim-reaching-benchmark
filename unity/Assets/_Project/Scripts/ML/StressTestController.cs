using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Stress-test framework for the dissertation A/B evaluation.
/// Attach to a GameObject in the scene alongside EpisodeResetter and DexterousPickPlaceAgent.
/// Set <see cref="enabled"/> = false for normal training; enable only during stress-test evaluation runs.
///
/// Eight conditions cycle automatically.  Results are written to
/// Application.persistentDataPath/stress_test_results.csv at the end of each episode.
/// </summary>
public class StressTestController : MonoBehaviour
{
    // ── condition definition ────────────────────────────────────────────────────
    [Serializable]
    public struct StressCondition
    {
        public string conditionName;
        [Tooltip("Max absolute XZ offset applied uniformly at random in [-offset, +offset].")]
        public float spawnOffsetRadius;   // metres
        [Tooltip("Gaussian noise std-dev added to all continuous observations.")]
        public float observationNoiseSigma;
    }

    // ── inspector ───────────────────────────────────────────────────────────────
    [Header("Conditions")]
    public List<StressCondition> conditions = new List<StressCondition>
    {
        new StressCondition { conditionName = "T1_Fixed_Baseline",       spawnOffsetRadius = 0.00f, observationNoiseSigma = 0.00f },
        new StressCondition { conditionName = "T2_Perturbation_3cm",     spawnOffsetRadius = 0.03f, observationNoiseSigma = 0.00f },
        new StressCondition { conditionName = "T3_Perturbation_6cm",     spawnOffsetRadius = 0.06f, observationNoiseSigma = 0.00f },
        new StressCondition { conditionName = "T4_Perturbation_10cm",    spawnOffsetRadius = 0.10f, observationNoiseSigma = 0.00f },
        new StressCondition { conditionName = "T5_Perturbation_15cm",    spawnOffsetRadius = 0.15f, observationNoiseSigma = 0.00f },
        new StressCondition { conditionName = "T6_Jitter_Low",           spawnOffsetRadius = 0.00f, observationNoiseSigma = 0.02f },
        new StressCondition { conditionName = "T7_Jitter_High",          spawnOffsetRadius = 0.00f, observationNoiseSigma = 0.05f },
        new StressCondition { conditionName = "T8_Combined_6cm_Jitter",  spawnOffsetRadius = 0.06f, observationNoiseSigma = 0.02f },
    };

    [Header("Episodes per condition")]
    [Tooltip("How many evaluation episodes to run per condition before moving to the next.")]
    public int episodesPerCondition = 50;

    [Header("Scene references")]
    [Tooltip("The transform used as the cube spawn origin (from EpisodeResetter).")]
    public Transform cubeSpawnTransform;

    [Header("Output")]
    [Tooltip("Leave empty to use Application.persistentDataPath/stress_test_results.csv")]
    public string csvOutputPath = "";

    // ── runtime state ───────────────────────────────────────────────────────────
    int _conditionIndex;
    int _episodeInCondition;
    int _totalEpisode;
    Vector3 _originalSpawnLocalPos;
    bool _initialised;
    StreamWriter _writer;

    // Applied offset for the current episode (read by DexterousPickPlaceAgent for obs noise)
    public float CurrentNoiseSigma { get; private set; }

    // ── lifecycle ───────────────────────────────────────────────────────────────
    void Awake()
    {
        if (!enabled) return;
        Init();
    }

    void Init()
    {
        if (_initialised) return;
        _initialised = true;

        if (cubeSpawnTransform != null)
            _originalSpawnLocalPos = cubeSpawnTransform.localPosition;

        string path = string.IsNullOrEmpty(csvOutputPath)
            ? Path.Combine(Application.persistentDataPath, "stress_test_results.csv")
            : csvOutputPath;

        bool writeHeader = !File.Exists(path);
        _writer = new StreamWriter(path, append: true, encoding: Encoding.UTF8);
        if (writeHeader)
            _writer.WriteLine("episode,condition_id,condition_name,spawn_offset_radius_m,obs_noise_sigma,success,time_to_success_s,timed_out,min_dist_to_place_m");
        _writer.Flush();

        Debug.Log($"[StressTest] Initialised — {conditions.Count} conditions × {episodesPerCondition} eps. Output: {path}");
    }

    void OnDestroy()
    {
        _writer?.Close();
    }

    // ── public API (called by EpisodeResetter and DexterousPickPlaceAgent) ──────

    /// <summary>
    /// Called by EpisodeResetter.ResetEpisode() BEFORE the cube position is written.
    /// Applies the spawn offset for the current condition.
    /// </summary>
    public void PreReset()
    {
        if (!enabled) return;
        if (!_initialised) Init();

        var cond = CurrentCondition();
        CurrentNoiseSigma = cond.observationNoiseSigma;

        if (cubeSpawnTransform != null)
        {
            float r = cond.spawnOffsetRadius;
            float dx = r > 0f ? UnityEngine.Random.Range(-r, r) : 0f;
            float dz = r > 0f ? UnityEngine.Random.Range(-r, r) : 0f;
            cubeSpawnTransform.localPosition = _originalSpawnLocalPos + new Vector3(dx, 0f, dz);
        }

        Debug.Log($"[StressTest] Episode {_totalEpisode + 1} — {cond.conditionName}  offset±{cond.spawnOffsetRadius * 100f:F0}cm  noise σ={cond.observationNoiseSigma}");
    }

    /// <summary>
    /// Called by DexterousPickPlaceAgent at the end of each episode with the result metrics.
    /// Advances to the next condition after episodesPerCondition episodes.
    /// </summary>
    public void RecordEpisode(bool success, float timeToSuccess, bool timedOut, float minDistToPlace)
    {
        if (!enabled) return;
        if (!_initialised) Init();

        var cond = CurrentCondition();
        string line = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
            _totalEpisode + 1,
            _conditionIndex,
            cond.conditionName,
            cond.spawnOffsetRadius.ToString("F4"),
            cond.observationNoiseSigma.ToString("F4"),
            success ? 1 : 0,
            success ? timeToSuccess.ToString("F3") : "",
            timedOut ? 1 : 0,
            minDistToPlace.ToString("F4"));

        _writer.WriteLine(line);
        _writer.Flush();

        _episodeInCondition++;
        _totalEpisode++;

        if (_episodeInCondition >= episodesPerCondition)
        {
            _episodeInCondition = 0;
            _conditionIndex++;

            if (_conditionIndex >= conditions.Count)
            {
                Debug.Log("[StressTest] All conditions complete. Stress test finished.");
                _conditionIndex = conditions.Count - 1; // stay on last condition rather than wrap
                enabled = false;
            }
            else
            {
                Debug.Log($"[StressTest] Moving to condition {_conditionIndex}: {conditions[_conditionIndex].conditionName}");
            }
        }
    }

    /// <summary>
    /// Adds Gaussian noise to a float observation value for the current condition.
    /// </summary>
    public float Noise(float value)
    {
        if (!enabled || CurrentNoiseSigma <= 0f) return value;
        return value + SampleGaussian(CurrentNoiseSigma);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────
    StressCondition CurrentCondition() =>
        conditions.Count > 0 ? conditions[Mathf.Clamp(_conditionIndex, 0, conditions.Count - 1)]
                              : new StressCondition { conditionName = "Default", spawnOffsetRadius = 0f, observationNoiseSigma = 0f };

    static float SampleGaussian(float sigma)
    {
        // Box-Muller transform
        float u1 = 1f - UnityEngine.Random.value;
        float u2 = 1f - UnityEngine.Random.value;
        return sigma * Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
    }
}
