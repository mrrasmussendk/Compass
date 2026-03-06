namespace VitruvianAbstractions.Interfaces;

/// <summary>
/// Shared command execution abstraction for modules that need to invoke external processes.
/// </summary>
public interface ICommandRunner
{
    /// <summary>
    /// Executes a command with arguments in a specific working directory.
    /// </summary>
    /// <param name="command">The command or executable to run.</param>
    /// <param name="args">The arguments to pass to the command.</param>
    /// <param name="workingDirectory">The working directory for command execution.</param>
    /// <param name="timeout">Maximum time to wait for command completion.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A result indicating success or failure of the command execution.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="ArgumentException">Thrown when timeout is zero or negative.</exception>
    Task<CommandExecutionResult> ExecuteAsync(
        string command,
        IReadOnlyList<string> args,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from running a command through <see cref="ICommandRunner"/>.
/// Represents one of three possible outcomes: successful execution, timeout, or start failure.
/// </summary>
public abstract record CommandExecutionResult
{
    private CommandExecutionResult() { }

    /// <summary>
    /// Command executed successfully.
    /// </summary>
    public sealed record Success(
        int ExitCode,
        string StandardOutput,
        string StandardError) : CommandExecutionResult;

    /// <summary>
    /// Command execution timed out.
    /// </summary>
    public sealed record Timeout : CommandExecutionResult;

    /// <summary>
    /// Command failed to start.
    /// </summary>
    public sealed record StartFailure(string ErrorMessage) : CommandExecutionResult;
}
