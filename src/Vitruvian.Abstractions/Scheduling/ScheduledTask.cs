namespace VitruvianAbstractions.Scheduling;

/// <summary>
/// Represents a task scheduled for future or repeated execution.
/// Users can describe repeat intervals in plain text (e.g. "every 5 minutes", "daily at 9am")
/// which are parsed by the LLM into a concrete <see cref="RepeatInterval"/>.
/// </summary>
public sealed class ScheduledTask
{
    /// <summary>Gets the unique identifier for this scheduled task.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the user request text to execute when the task fires.</summary>
    public required string Request { get; init; }

    /// <summary>Gets or sets the original plain-text schedule description provided by the user (e.g. "every 30 minutes").</summary>
    public string? ScheduleDescription { get; init; }

    /// <summary>Gets or sets the parsed repeat interval. Null means a one-shot task.</summary>
    public TimeSpan? RepeatInterval { get; set; }

    /// <summary>Gets or sets the next UTC time this task should fire.</summary>
    public DateTimeOffset NextRunUtc { get; set; }

    /// <summary>Gets or sets whether this task is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the UTC time this task was created.</summary>
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the UTC time this task last executed, or null if it has not run yet.</summary>
    public DateTimeOffset? LastRunUtc { get; set; }

    /// <summary>Gets or sets the number of times this task has been executed.</summary>
    public int RunCount { get; set; }
}
