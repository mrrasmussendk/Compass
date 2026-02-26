using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Memory;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Orchestration;

namespace UtilityAi.Compass.Runtime.Modules;

/// <summary>
/// Records executed proposals and last-winner state into <see cref="IMemoryStore"/>.
/// Implements both <see cref="ICapabilityModule"/> and <see cref="IOrchestrationSink"/>.
/// </summary>
public sealed class GovernanceFinalizerModule : ICapabilityModule, IOrchestrationSink
{
    private readonly IMemoryStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="GovernanceFinalizerModule"/> class.
    /// </summary>
    /// <param name="store">The memory store used to persist governance state.</param>
    public GovernanceFinalizerModule(IMemoryStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(UtilityAi.Utils.Runtime rt) => [];

    /// <inheritdoc />
    void IOrchestrationSink.OnTickStart(UtilityAi.Utils.Runtime rt) { }
    /// <inheritdoc />
    void IOrchestrationSink.OnScored(UtilityAi.Utils.Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored) { }
    /// <inheritdoc />
    void IOrchestrationSink.OnChosen(UtilityAi.Utils.Runtime rt, Proposal chosen, double utility) { }
    /// <inheritdoc />
    void IOrchestrationSink.OnStopped(UtilityAi.Utils.Runtime rt, OrchestrationStopReason reason) { }

    /// <inheritdoc />
    void IOrchestrationSink.OnActed(UtilityAi.Utils.Runtime rt, Proposal chosen)
    {
        var corrId = rt.Bus.GetOrDefault<CorrelationId>()?.Value;
        var now = DateTimeOffset.UtcNow;

        _ = Task.Run(async () =>
        {
            var record = new ProposalExecutionRecord(chosen.Id, corrId, now, 0.0);
            await _store.StoreAsync(record, now);
            await _store.StoreAsync(new LastWinner(chosen.Id, now), now);
        });
    }
}
