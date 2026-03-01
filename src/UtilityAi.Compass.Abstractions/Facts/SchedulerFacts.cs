namespace UtilityAi.Compass.Abstractions.Facts;

/// <summary>
/// Represents a scheduled job that should be executed at regular intervals.
/// Stored in the memory store so jobs survive restarts.
/// </summary>
/// <param name="JobId">Unique identifier for the scheduled job.</param>
/// <param name="Command">The command string to execute when the job fires.</param>
/// <param name="IntervalSeconds">Interval in seconds between executions.</param>
/// <param name="CreatedAt">Timestamp when the job was first created.</param>
/// <param name="Enabled">Whether the job is currently active.</param>
public sealed record ScheduledJob(
    string JobId,
    string Command,
    int IntervalSeconds,
    DateTimeOffset CreatedAt,
    bool Enabled = true
);

/// <summary>
/// Execution log entry recorded after a scheduled job has been executed.
/// Stored in the memory store so run history is durable.
/// </summary>
/// <param name="JobId">Identifier of the scheduled job that was executed.</param>
/// <param name="ExecutedAt">Timestamp when the job was executed.</param>
/// <param name="Success">Whether the execution completed successfully.</param>
/// <param name="Output">Optional output or error message from the execution.</param>
public sealed record ScheduledJobRun(
    string JobId,
    DateTimeOffset ExecutedAt,
    bool Success,
    string? Output = null
);

/// <summary>
/// Fact published by the scheduler sensor when one or more jobs are due for execution.
/// </summary>
/// <param name="DueJobs">The list of jobs that are due to run.</param>
public sealed record ScheduledJobsDue(IReadOnlyList<ScheduledJob> DueJobs);

/// <summary>
/// Fact published when a new scheduled job has been added, signalling that
/// the active job list should be refreshed.
/// </summary>
/// <param name="Job">The newly added job.</param>
public sealed record ScheduledJobAdded(ScheduledJob Job);
