using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Hitl.Modules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class HitlGateModuleTests
{
    [Fact]
    public void Propose_CreatesHitlRequest_ForDeployRequest()
    {
        var module = new HitlGateModule(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        bus.Publish(new UserRequest("deploy this service"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("hitl.create-request", proposals[0].Id);
    }

    [Fact]
    public void Propose_DoesNotCreateHitlRequest_ForSocketDeployRequest()
    {
        var module = new HitlGateModule(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        bus.Publish(new UserRequest("allow deploy for a socket connection"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    private sealed class NoopHumanDecisionChannel : IHumanDecisionChannel
    {
        public Task SendRequestAsync(string requestId, string description, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool?> TryReceiveDecisionAsync(string requestId, CancellationToken ct = default)
            => Task.FromResult<bool?>(null);
    }
}
