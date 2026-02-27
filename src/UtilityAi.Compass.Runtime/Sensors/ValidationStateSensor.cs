using UtilityAi.Memory;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.Runtime.Sensors;

/// <summary>
/// Projects validation state from <see cref="IMemoryStore"/> onto the EventBus each tick.
/// Publishes <see cref="NeedsValidation"/> and <see cref="ValidationOutcome"/> facts
/// based on the latest workflow step records.
/// </summary>
public sealed class ValidationStateSensor : ISensor
{
    private readonly IMemoryStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationStateSensor"/> class.
    /// </summary>
    /// <param name="store">The memory store used to recall validation state.</param>
    public ValidationStateSensor(IMemoryStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        var active = rt.Bus.GetOrDefault<ActiveWorkflow>();
        if (active is null) return;

        // Check for pending validation requests
        var validationRequests = await _store.RecallAsync<NeedsValidation>(
            new MemoryQuery { MaxResults = 1, SortOrder = SortOrder.NewestFirst }, ct);

        var pending = validationRequests
            .FirstOrDefault(v => v.Fact.RunId == active.RunId);

        if (pending is not null)
        {
            rt.Bus.Publish(pending.Fact);

            // Check if a validation result already exists for this target
            var results = await _store.RecallAsync<ValidationRecord>(
                new MemoryQuery { MaxResults = 10, SortOrder = SortOrder.NewestFirst }, ct);

            var result = results.FirstOrDefault(r =>
                r.Fact.RunId == active.RunId && r.Fact.TargetId == pending.Fact.TargetId);

            if (result is not null)
            {
                rt.Bus.Publish(new ValidationOutcome(result.Fact.Outcome, result.Fact.Diagnostics));
            }
        }
    }
}
