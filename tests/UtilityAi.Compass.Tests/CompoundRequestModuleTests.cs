using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.StandardModules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

/// <summary>
/// Tests for the <see cref="CompoundRequestModule"/>, which serves as a fallback
/// when no model client is available to decompose compound requests at the host level.
/// </summary>
public class CompoundRequestModuleTests
{
    [Fact]
    public void Propose_ReturnsEmpty_WhenNoMultiStepRequest()
    {
        var module = new CompoundRequestModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file test.txt with content hello"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public void Propose_ReturnsEmpty_WhenMultiStepIsNotCompound()
    {
        var module = new CompoundRequestModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file test.txt with content hello"));
        bus.Publish(new MultiStepRequest("create file test.txt with content hello", 1, IsCompound: false));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public void Propose_ReturnsGuidance_WhenCompoundDetected()
    {
        var module = new CompoundRequestModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file u.txt then tell me about rainbows"));
        bus.Publish(new MultiStepRequest("create file u.txt then tell me about rainbows", 2, IsCompound: true));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("compound-request.respond", proposals[0].Id);
    }

    [Fact]
    public async Task Propose_GuidanceResponse_ContainsHelpfulMessage()
    {
        var module = new CompoundRequestModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file u.txt then tell me about rainbows"));
        bus.Publish(new MultiStepRequest("create file u.txt then tell me about rainbows", 2, IsCompound: true));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        await proposals[0].Act(CancellationToken.None);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        Assert.Contains("multiple independent tasks", response.Text);
    }

    [Fact]
    public void Propose_HasPositiveUtility()
    {
        var module = new CompoundRequestModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file u.txt then tell me about rainbows"));
        bus.Publish(new MultiStepRequest("create file u.txt then tell me about rainbows", 2, IsCompound: true));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);
        Assert.True(proposals[0].Utility(rt) > 0);
    }
}
