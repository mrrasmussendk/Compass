namespace UtilityAi.Compass.Abstractions.CliAction;

/// <summary>
/// Represents a discoverable command-line action that can be routed
/// through the UtilityAI intent detection pipeline.
/// </summary>
/// <remarks>
/// Implement this interface in plugins or host modules to register
/// read, write, or update operations that are automatically discovered,
/// scored, and selected by the governance strategy.
/// </remarks>
public interface ICliAction
{
    /// <summary>The CLI verb this action handles (Read, Write, or Update).</summary>
    CliVerb Verb { get; }

    /// <summary>The route or resource name this action targets (e.g. "config", "users").</summary>
    string Route { get; }

    /// <summary>Human-readable description of the action.</summary>
    string Description { get; }

    /// <summary>Executes the CLI action with the given input text.</summary>
    Task<string> ExecuteAsync(string input, CancellationToken ct = default);
}
