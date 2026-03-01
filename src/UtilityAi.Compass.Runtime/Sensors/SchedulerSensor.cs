using UtilityAi.Memory;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.Runtime.Sensors;

/// <summary>
/// Sensor that queries <see cref="IMemoryStore"/> for <see cref="ScheduledJob"/> records,
/// determines which are due based on their interval and last run time, and publishes
/// a <see cref="ScheduledJobsDue"/> fact onto the EventBus.
/// </summary>
public sealed class SchedulerSensor : ISensor
{
    private readonly IMemoryStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulerSensor"/> class.
    /// </summary>
    /// <param name="store">The memory store used to recall scheduled jobs and run history.</param>
    public SchedulerSensor(IMemoryStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        var jobs = await _store.RecallAsync<ScheduledJob>(
            new MemoryQuery { MaxResults = 1000, SortOrder = SortOrder.NewestFirst }, ct);

        // Deduplicate by JobId – keep only the most recent version of each job.
        var latestJobs = new Dictionary<string, ScheduledJob>();
        foreach (var entry in jobs)
        {
            latestJobs.TryAdd(entry.Fact.JobId, entry.Fact);
        }

        var now = DateTimeOffset.UtcNow;
        var dueJobs = new List<ScheduledJob>();

        foreach (var job in latestJobs.Values)
        {
            if (!job.Enabled) continue;

            var runs = await _store.RecallAsync<ScheduledJobRun>(
                new MemoryQuery { MaxResults = 100, SortOrder = SortOrder.NewestFirst }, ct);

            var lastRun = runs.FirstOrDefault(r => r.Fact.JobId == job.JobId);
            if (lastRun is null)
            {
                // Never run – it's due.
                dueJobs.Add(job);
                continue;
            }

            var elapsed = now - lastRun.Fact.ExecutedAt;
            if (elapsed >= TimeSpan.FromSeconds(job.IntervalSeconds))
            {
                dueJobs.Add(job);
            }
        }

        if (dueJobs.Count > 0)
        {
            rt.Bus.Publish(new ScheduledJobsDue(dueJobs));
        }
    }
}
