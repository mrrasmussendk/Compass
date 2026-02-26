using UtilityAi.Memory;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.Runtime.Sensors;

/// <summary>
/// Projects governance state (<see cref="LastWinner"/> and <see cref="CooldownState"/>)
/// from <see cref="IMemoryStore"/> onto the EventBus each tick.
/// </summary>
public sealed class GovernanceMemoryProjectionSensor : ISensor
{
    private readonly IMemoryStore _store;
    private readonly IReadOnlyList<string> _cooldownKeys;

    /// <summary>
    /// Initializes a new instance of the <see cref="GovernanceMemoryProjectionSensor"/> class.
    /// </summary>
    /// <param name="store">The memory store used to recall governance state.</param>
    /// <param name="cooldownKeys">Optional list of proposal IDs whose cooldown state should be projected.</param>
    public GovernanceMemoryProjectionSensor(IMemoryStore store, IReadOnlyList<string>? cooldownKeys = null)
    {
        _store = store;
        _cooldownKeys = cooldownKeys ?? Array.Empty<string>();
    }

    /// <inheritdoc />
    public async Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        var winners = await _store.RecallAsync<LastWinner>(new MemoryQuery { MaxResults = 1 }, ct);
        if (winners.Count > 0)
            rt.Bus.Publish(winners[0].Fact);

        var now = DateTimeOffset.UtcNow;
        foreach (var key in _cooldownKeys)
        {
            var records = await _store.RecallAsync<ProposalExecutionRecord>(
                new MemoryQuery { MaxResults = 10, SortOrder = SortOrder.NewestFirst }, ct);

            var recent = records.FirstOrDefault(r => r.Fact.ProposalId == key);
            if (recent is null)
            {
                rt.Bus.Publish(new CooldownState(key, false));
                continue;
            }

            var ttl = TimeSpan.FromSeconds(30);
            var remaining = ttl - (now - recent.Fact.ExecutedAt);
            rt.Bus.Publish(new CooldownState(key, remaining > TimeSpan.Zero, remaining > TimeSpan.Zero ? remaining : null));
        }
    }
}
