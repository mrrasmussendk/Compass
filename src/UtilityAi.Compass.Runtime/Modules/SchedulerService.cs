using System.Diagnostics;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Memory;

namespace UtilityAi.Compass.Runtime.Modules;

/// <summary>
/// Background service that polls the <see cref="IMemoryStore"/> at a configurable
/// interval, finds <see cref="ScheduledJob"/> records that are due, executes each
/// command on a separate thread, and logs <see cref="ScheduledJobRun"/> results.
/// </summary>
public sealed class SchedulerService : IDisposable
{
    private readonly IMemoryStore _store;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runLoopTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulerService"/> class.
    /// </summary>
    /// <param name="store">The memory store used to read jobs and write run logs.</param>
    /// <param name="pollInterval">How often to poll for due jobs. Defaults to 15 seconds.</param>
    public SchedulerService(IMemoryStore store, TimeSpan? pollInterval = null)
    {
        _store = store;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Starts the background polling loop.
    /// </summary>
    public void Start()
    {
        if (_runLoopTask is not null) return;
        _runLoopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Stops the background polling loop gracefully.
    /// </summary>
    public async Task StopAsync()
    {
        _cts.Cancel();
        if (_runLoopTask is not null)
            await _runLoopTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ExecuteDueJobsAsync(ct);
                await Task.Delay(_pollInterval, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task ExecuteDueJobsAsync(CancellationToken ct)
    {
        var allJobs = await _store.RecallAsync<ScheduledJob>(
            new MemoryQuery { MaxResults = 1000, SortOrder = SortOrder.NewestFirst }, ct);

        // Deduplicate by JobId – keep only the most recent version.
        var latestJobs = new Dictionary<string, ScheduledJob>();
        foreach (var entry in allJobs)
        {
            latestJobs.TryAdd(entry.Fact.JobId, entry.Fact);
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var job in latestJobs.Values)
        {
            if (!job.Enabled) continue;

            var runs = await _store.RecallAsync<ScheduledJobRun>(
                new MemoryQuery { MaxResults = 100, SortOrder = SortOrder.NewestFirst }, ct);

            var lastRun = runs.FirstOrDefault(r => r.Fact.JobId == job.JobId);
            if (lastRun is not null)
            {
                var elapsed = now - lastRun.Fact.ExecutedAt;
                if (elapsed < TimeSpan.FromSeconds(job.IntervalSeconds))
                    continue;
            }

            // Execute on a separate thread so one slow job doesn't block others.
            _ = Task.Run(async () => await ExecuteJobAsync(job), ct);
        }
    }

    public async Task ExecuteJobAsync(ScheduledJob job)
    {
        var now = DateTimeOffset.UtcNow;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                Arguments = OperatingSystem.IsWindows() ? $"/c {job.Command}" : $"-c \"{job.Command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                await _store.StoreAsync(
                    new ScheduledJobRun(job.JobId, now, Success: false, Output: "Failed to start process."),
                    now);
                return;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var success = process.ExitCode == 0;
            var combinedOutput = string.IsNullOrWhiteSpace(error) ? output : $"{output}\n{error}";

            await _store.StoreAsync(
                new ScheduledJobRun(job.JobId, now, success, combinedOutput.Trim()),
                now);
        }
        catch (Exception ex)
        {
            await _store.StoreAsync(
                new ScheduledJobRun(job.JobId, now, Success: false, Output: ex.Message),
                now);
        }
    }
}
