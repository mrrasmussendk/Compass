using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Memory;

namespace UtilityAi.Compass.Runtime.Modules;

/// <summary>
/// Capability module that allows users to schedule commands for periodic execution.
/// Commands are persisted in the <see cref="IMemoryStore"/> and executed by the
/// <see cref="SchedulerService"/> on a background thread.
/// </summary>
public sealed class SchedulerModule : ICapabilityModule
{
    private readonly IMemoryStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulerModule"/> class.
    /// </summary>
    /// <param name="store">The memory store used to persist scheduled jobs.</param>
    public SchedulerModule(IMemoryStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(UtilityAi.Utils.Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        var text = request.Text;

        if (IsScheduleRequest(text))
        {
            yield return new Proposal(
                id: "scheduler.add",
                cons: [new ConstantValue(0.8)],
                act: async _ =>
                {
                    var (command, intervalSeconds) = ParseScheduleRequest(text);
                    var job = new ScheduledJob(
                        JobId: Guid.NewGuid().ToString("N"),
                        Command: command,
                        IntervalSeconds: intervalSeconds,
                        CreatedAt: DateTimeOffset.UtcNow);

                    await _store.StoreAsync(job, DateTimeOffset.UtcNow);
                    rt.Bus.Publish(new ScheduledJobAdded(job));
                    rt.Bus.Publish(new AiResponse(
                        $"Scheduled command '{command}' to run every {intervalSeconds}s (job {job.JobId})."));
                }
            ) { Description = "Schedule a command to run at a regular interval" };
        }
    }

    public static bool IsScheduleRequest(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("schedule") || lower.Contains("every") || lower.Contains("interval");
    }

    public static (string Command, int IntervalSeconds) ParseScheduleRequest(string text)
    {
        // Expected patterns:
        //   "schedule 'echo hello' every 60s"
        //   "schedule \"echo hello\" every 120"
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = string.Empty;
        var intervalSeconds = 60; // default

        var inQuote = false;
        var quoteChar = '\'';
        var commandParts = new List<string>();
        var foundEvery = false;

        foreach (var token in tokens)
        {
            if (foundEvery)
            {
                var cleaned = token.TrimEnd('s', 'S');
                if (int.TryParse(cleaned, out var parsed) && parsed > 0)
                    intervalSeconds = parsed;
                continue;
            }

            if (!inQuote && (token.StartsWith('\'') || token.StartsWith('"')))
            {
                quoteChar = token[0];
                inQuote = true;
                var part = token.TrimStart(quoteChar);
                if (part.EndsWith(quoteChar))
                {
                    part = part.TrimEnd(quoteChar);
                    inQuote = false;
                }
                commandParts.Add(part);
                continue;
            }

            if (inQuote)
            {
                if (token.EndsWith(quoteChar))
                {
                    commandParts.Add(token.TrimEnd(quoteChar));
                    inQuote = false;
                }
                else
                {
                    commandParts.Add(token);
                }
                continue;
            }

            if (token.Equals("every", StringComparison.OrdinalIgnoreCase))
            {
                foundEvery = true;
            }
        }

        command = commandParts.Count > 0 ? string.Join(' ', commandParts) : "echo scheduled";
        return (command, intervalSeconds);
    }
}
