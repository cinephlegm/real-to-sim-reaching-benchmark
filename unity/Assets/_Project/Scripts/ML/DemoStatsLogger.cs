using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Logs per-episode stats for both human demo recording and BC/PPO training runs.
/// Columns: episode, timestamp, success, duration_seconds, grasp_validated, lifted, dropped_floor, min_dist_to_place,
///          time_to_grasp, time_to_lift,
///          avg_thumb, avg_index, avg_middle, avg_ring, avg_pinky, avg_spread, avg_wrist_roll
/// Saved to &lt;project_root&gt;/Demos/demo_stats.csv.
/// RecordStep() must be called every FixedUpdate step by the agent.
/// ResetAccumulators() must be called at OnEpisodeBegin.
/// </summary>
public class DemoStatsLogger : MonoBehaviour
{
    static readonly string CsvPath = Path.Combine(
        Application.dataPath, "..", "Demos", "demo_stats.csv");

    static readonly string DemosDir = Path.Combine(
        Application.dataPath, "..", "Demos");

    // Per-episode accumulators (reset each episode)
    int _stepCount;
    double _sumT, _sumI, _sumM, _sumR, _sumP, _sumSp, _sumWr;

    // Shared across all instances (multi-arena safe): each LogEpisode call gets a unique number
    static int _sharedNextEpisode = -1;

    void Awake()
    {
        EnsureHeader();
        // First instance initialises from demo file count; subsequent instances share the same counter
        if (_sharedNextEpisode < 0)
            _sharedNextEpisode = CountDemoFiles();
    }

    /// <summary>Returns the number of .demo files currently in the Demos folder.</summary>
    static int CountDemoFiles()
    {
        try
        {
            if (!Directory.Exists(DemosDir)) return 0;
            return Directory.GetFiles(DemosDir, "*.demo").Length;
        }
        catch { return 0; }
    }

    /// <summary>Call once per FixedUpdate step from the agent to accumulate hand pose data.</summary>
    public void RecordStep(float thumb, float index, float middle, float ring,
                           float pinky, float spread, float wristRoll)
    {
        _stepCount++;
        _sumT += thumb;
        _sumI += index;
        _sumM += middle;
        _sumR += ring;
        _sumP += pinky;
        _sumSp += spread;
        _sumWr += wristRoll;
    }

    /// <summary>Call at OnEpisodeBegin to clear accumulators for the new episode.</summary>
    public void ResetAccumulators()
    {
        _stepCount = 0;
        _sumT = _sumI = _sumM = _sumR = _sumP = _sumSp = _sumWr = 0.0;
    }

    /// <summary>Call at episode end with all outcome data.</summary>
    public void LogEpisode(bool success, float durationSeconds,
                           bool graspValidated, bool lifted, bool droppedFloor, float minDistToPlace,
                           float timeToGrasp, float timeToLift)
    {
        int episode = _sharedNextEpisode++;
        double n = _stepCount > 0 ? _stepCount : 1;

        string row = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0},{1},{2},{3:F2},{4},{5},{6},{7:F4},{8:F2},{9:F2},{10:F3},{11:F3},{12:F3},{13:F3},{14:F3},{15:F3},{16:F3}",
            episode,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            success ? 1 : 0,
            durationSeconds,
            graspValidated ? 1 : 0,
            lifted ? 1 : 0,
            droppedFloor ? 1 : 0,
            minDistToPlace,
            timeToGrasp,
            timeToLift,
            _sumT / n,
            _sumI / n,
            _sumM / n,
            _sumR / n,
            _sumP / n,
            _sumSp / n,
            _sumWr / n);

        try
        {
            UpsertRow(episode, row);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DemoStatsLogger] Could not write CSV: " + e.Message);
        }
    }

    /// <summary>Overwrites the row with the given episode number if it exists, otherwise appends.</summary>
    static void UpsertRow(int episode, string newRow)
    {
        if (!File.Exists(CsvPath))
        {
            File.AppendAllText(CsvPath, newRow + Environment.NewLine);
            return;
        }

        string[] lines = File.ReadAllLines(CsvPath);
        bool found = false;
        for (int i = 1; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            string first = trimmed.Split(',')[0];
            if (int.TryParse(first, out int ep) && ep == episode)
            {
                lines[i] = newRow;
                found = true;
                break;
            }
        }

        if (found)
            File.WriteAllLines(CsvPath, lines);
        else
            File.AppendAllText(CsvPath, newRow + Environment.NewLine);
    }

    void EnsureHeader()
    {
        try
        {
            string dir = Path.GetDirectoryName(CsvPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(CsvPath))
                File.WriteAllText(CsvPath,
                    "episode,timestamp,success,duration_seconds," +
                    "grasp_validated,lifted,dropped_floor,min_dist_to_place," +
                    "time_to_grasp,time_to_lift," +
                    "avg_thumb,avg_index,avg_middle,avg_ring,avg_pinky,avg_spread,avg_wrist_roll"
                    + Environment.NewLine);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DemoStatsLogger] Could not create CSV: " + e.Message);
        }
    }
}
