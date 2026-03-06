using System.Diagnostics;
using VitruvianAbstractions.Interfaces;

namespace VitruvianAbstractions;

/// <summary>
/// Default <see cref="ICommandRunner"/> implementation backed by <see cref="Process"/>.
/// </summary>
public sealed class ProcessCommandRunner : ICommandRunner
{
    public async Task<CommandExecutionResult> ExecuteAsync(
        string command,
        IReadOnlyList<string> args,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new CommandExecutionResult.StartFailure(ex.Message);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                return new CommandExecutionResult.Timeout();
            }

            throw;
        }

        return new CommandExecutionResult.Success(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }
}
