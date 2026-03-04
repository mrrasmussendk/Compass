namespace VitruvianAbstractions.Scheduling;

/// <summary>
/// Abstraction for persisting and querying scheduled tasks.
/// Implementations may use in-memory storage, SQLite, or other backing stores.
/// </summary>
public interface IScheduledTaskStore
{
    /// <summary>Adds a new scheduled task.</summary>
    Task AddAsync(ScheduledTask task, CancellationToken ct = default);

    /// <summary>Removes a scheduled task by its identifier.</summary>
    Task<bool> RemoveAsync(string taskId, CancellationToken ct = default);

    /// <summary>Returns all tasks whose <see cref="ScheduledTask.NextRunUtc"/> is at or before the specified time and are enabled.</summary>
    Task<IReadOnlyList<ScheduledTask>> GetDueTasksAsync(DateTimeOffset asOf, CancellationToken ct = default);

    /// <summary>Returns all scheduled tasks.</summary>
    Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Updates an existing scheduled task (e.g. after advancing <see cref="ScheduledTask.NextRunUtc"/>).</summary>
    Task UpdateAsync(ScheduledTask task, CancellationToken ct = default);
}
