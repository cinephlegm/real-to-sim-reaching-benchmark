using Unity.MLAgents;
using UnityEngine;

public class MetricsReporter : MonoBehaviour
{
    const string SuccessRateKey = "Task/SuccessRate";
    const string TimeToSuccessKey = "Task/TimeToSuccess";
    const string ValidatedGraspKey = "Task/ValidatedGrasp";
    const string DropCountKey = "Task/DropCount";
    const string EpisodeLengthKey = "Task/EpisodeLength";
    const string EpisodeTimeoutKey = "Task/TimeoutRate";

    public void LogStat(
        string key,
        float value,
        StatAggregationMethod aggregation = StatAggregationMethod.Average)
    {
        Academy.Instance.StatsRecorder.Add(key, value, aggregation);
    }

    public void LogEpisodeResult(bool success, float episodeSeconds, bool timedOut, bool validatedGrasped)
    {
        LogStat(SuccessRateKey, success ? 1f : 0f, StatAggregationMethod.Average);
        LogStat(ValidatedGraspKey, validatedGrasped ? 1f : 0f, StatAggregationMethod.Average);
        LogStat(EpisodeLengthKey, episodeSeconds, StatAggregationMethod.Average);
        LogStat(EpisodeTimeoutKey, timedOut ? 1f : 0f, StatAggregationMethod.Average);

        if (success)
            LogStat(TimeToSuccessKey, episodeSeconds, StatAggregationMethod.Average);
    }

    public void LogSuccess(bool success)
    {
        LogStat(SuccessRateKey, success ? 1f : 0f, StatAggregationMethod.Average);
    }

    public void LogValidatedGrasp(bool validated)
    {
        LogStat(ValidatedGraspKey, validated ? 1f : 0f, StatAggregationMethod.Average);
    }

    public void LogTimeToSuccess(float seconds)
    {
        LogStat(TimeToSuccessKey, seconds, StatAggregationMethod.Average);
    }

    public void LogEpisodeLength(float seconds)
    {
        LogStat(EpisodeLengthKey, seconds, StatAggregationMethod.Average);
    }

    public void LogTimeout(bool timedOut)
    {
        LogStat(EpisodeTimeoutKey, timedOut ? 1f : 0f, StatAggregationMethod.Average);
    }

    public void LogDrop()
    {
        LogStat(DropCountKey, 1f, StatAggregationMethod.Sum);
    }

    public void LogDrops(int count)
    {
        if (count > 0)
            LogStat(DropCountKey, count, StatAggregationMethod.Sum);
    }
}