using VitruvianAbstractions.Facts;

namespace VitruvianRuntime.DI;

/// <summary>
/// Configuration options for Compass DI registration via <see cref="ServiceCollectionExtensions.AddUtilityAiCompass"/>.
/// </summary>
public sealed class CompassOptions
{
    /// <summary>
    /// Gets or sets the working directory for file operations.
    /// When not set, uses the current directory. Relative file paths in file operations are resolved relative to this directory.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets the SQLite connection string used for durable memory.
    /// When not set, Compass uses a local <c>appdb/compass-memory.db</c> file under the app base directory.
    /// </summary>
    public string? MemoryConnectionString { get; set; }

    /// <summary>Gets or sets the <see cref="Facts.GovernanceConfig"/> used by the governance strategy.</summary>
    public GovernanceConfig GovernanceConfig { get; set; } = new();

    /// <summary>Gets or sets whether the <see cref="Modules.GovernanceFinalizerModule"/> is registered.</summary>
    public bool EnableGovernanceFinalizer { get; set; } = true;

    /// <summary>Gets or sets whether Human-in-the-Loop gating is enabled.</summary>
    public bool EnableHitl { get; set; } = false;

    /// <summary>Gets or sets the cooldown keys whose state is tracked each tick.</summary>
    public List<string> TrackedCooldownKeys { get; set; } = [];

    /// <summary>Gets or sets whether the scheduled-command module and its background service are registered.</summary>
    public bool EnableScheduler { get; set; } = false;

    /// <summary>Gets or sets the polling interval for the scheduler background service. Defaults to 15 seconds.</summary>
    public TimeSpan? SchedulerPollInterval { get; set; }

    /// <summary>Gets or sets the maximum number of conversation turns to keep in memory. Defaults to 10.</summary>
    public int MaxConversationTurns { get; set; } = 10;

    /// <summary>Gets or sets the context window size (number of prior step outputs) for plan execution. Defaults to 4.</summary>
    public int ContextWindowSize { get; set; } = 4;

    /// <summary>
    /// Router configuration settings.
    /// </summary>
    public RouterOptions Router { get; set; } = new();
}

/// <summary>
/// Configuration options for module routing.
/// </summary>
public sealed class RouterOptions
{
    /// <summary>Gets or sets the minimum confidence threshold for conversation module selection. Defaults to 0.3.</summary>
    public double ConversationConfidenceThreshold { get; set; } = 0.3;

    /// <summary>Gets or sets the minimum confidence threshold for specialized module selection. Defaults to 0.6.</summary>
    public double SpecializedConfidenceThreshold { get; set; } = 0.6;
}
