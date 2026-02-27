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

    [Theory]
    [InlineData("allow deploy for a socket connection")]
    [InlineData("allow deploy for a socket-connection")]
    [InlineData("allow deploy for socket connections")]
    public void Propose_DoesNotCreateHitlRequest_ForSocketDeployRequest(string requestText)
    {
        var module = new HitlGateModule(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        bus.Publish(new UserRequest(requestText));
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
