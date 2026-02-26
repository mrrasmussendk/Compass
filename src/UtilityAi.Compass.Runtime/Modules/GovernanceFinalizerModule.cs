using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Memory;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Orchestration;

namespace UtilityAi.Compass.Runtime.Modules;

public sealed class GovernanceFinalizerModule : ICapabilityModule, IOrchestrationSink
{
    private readonly IMemoryStore _store;

    public GovernanceFinalizerModule(IMemoryStore store)
    {
        _store = store;
    }

    public IEnumerable<Proposal> Propose(UtilityAi.Utils.Runtime rt) => [];

    void IOrchestrationSink.OnTickStart(UtilityAi.Utils.Runtime rt) { }
    void IOrchestrationSink.OnScored(UtilityAi.Utils.Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored) { }
    void IOrchestrationSink.OnChosen(UtilityAi.Utils.Runtime rt, Proposal chosen, double utility) { }
    void IOrchestrationSink.OnStopped(UtilityAi.Utils.Runtime rt, OrchestrationStopReason reason) { }

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
