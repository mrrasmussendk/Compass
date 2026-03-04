using VitruvianAbstractions.Scheduling;

namespace VitruvianRuntime.Scheduling;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IScheduledTaskStore"/>.
/// Suitable for single-process scenarios; tasks are lost on restart.
/// </summary>
public sealed class InMemoryScheduledTaskStore : IScheduledTaskStore
{
    private readonly List<ScheduledTask> _tasks = [];
    private readonly object _lock = new();

    public Task AddAsync(ScheduledTask task, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _tasks.Add(task);
        }
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string taskId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var index = _tasks.FindIndex(t => t.Id == taskId);
            if (index < 0)
                return Task.FromResult(false);
            _tasks.RemoveAt(index);
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<ScheduledTask>> GetDueTasksAsync(DateTimeOffset asOf, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var due = _tasks
                .Where(t => t.Enabled && t.NextRunUtc <= asOf)
                .ToList();
            return Task.FromResult<IReadOnlyList<ScheduledTask>>(due);
        }
    }

    public Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ScheduledTask>>(_tasks.ToList());
        }
    }

    public Task UpdateAsync(ScheduledTask task, CancellationToken ct = default)
    {
        // In-memory store uses reference equality; the caller already mutated the object.
        // No additional work needed.
        return Task.CompletedTask;
    }
}
