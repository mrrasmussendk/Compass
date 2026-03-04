using Microsoft.Extensions.Hosting;
using VitruvianAbstractions.Scheduling;
using VitruvianRuntime.DI;

namespace VitruvianRuntime.Scheduling;

/// <summary>
/// Background service that polls the <see cref="IScheduledTaskStore"/> at a configurable interval,
/// executes due tasks via a supplied callback, and advances their next-run times.
/// </summary>
public sealed class SchedulerService : BackgroundService
{
    private readonly IScheduledTaskStore _store;
    private readonly Func<string, CancellationToken, Task<string>> _executeRequest;
    private readonly TimeSpan _pollInterval;
    private readonly Action<string>? _log;

    /// <summary>
    /// Creates a new <see cref="SchedulerService"/>.
    /// </summary>
    /// <param name="store">The store that holds scheduled tasks.</param>
    /// <param name="executeRequest">
    ///   Callback that executes a user request and returns the response text.
    ///   This is typically wired to <c>RequestProcessor.ProcessAsync</c>.
    /// </param>
    /// <param name="options">Vitruvian options containing scheduler configuration.</param>
    /// <param name="log">Optional logging callback.</param>
    public SchedulerService(
        IScheduledTaskStore store,
        Func<string, CancellationToken, Task<string>> executeRequest,
        VitruvianOptions options,
        Action<string>? log = null)
    {
        _store = store;
        _executeRequest = executeRequest;
        _pollInterval = options.SchedulerPollInterval ?? TimeSpan.FromSeconds(15);
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log?.Invoke($"[Scheduler] Started. Polling every {_pollInterval.TotalSeconds}s.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueTasksAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[Scheduler] Error: {ex.Message}");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _log?.Invoke("[Scheduler] Stopped.");
    }

    /// <summary>
    /// Queries the store for due tasks, executes them, and advances their next-run times.
    /// Exposed internally for unit testing.
    /// </summary>
    internal async Task RunDueTasksAsync(CancellationToken ct)
    {
        var dueTasks = await _store.GetDueTasksAsync(DateTimeOffset.UtcNow, ct);

        foreach (var task in dueTasks)
        {
            _log?.Invoke($"[Scheduler] Executing task {task.Id}: {task.Request}");

            try
            {
                var response = await _executeRequest(task.Request, ct);
                _log?.Invoke($"[Scheduler] Task {task.Id} completed: {Truncate(response, 200)}");
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[Scheduler] Task {task.Id} failed: {ex.Message}");
            }

            task.LastRunUtc = DateTimeOffset.UtcNow;
            task.RunCount++;

            if (task.RepeatInterval.HasValue)
            {
                task.NextRunUtc = DateTimeOffset.UtcNow + task.RepeatInterval.Value;
                await _store.UpdateAsync(task, ct);
            }
            else
            {
                task.Enabled = false;
                await _store.UpdateAsync(task, ct);
            }
        }
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";
}
