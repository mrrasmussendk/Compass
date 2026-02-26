using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Utils;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Compass.PluginSdk.MetadataProvider;

namespace UtilityAi.Compass.Tests;

[CompassCapability("test-domain", priority: 5)]
[CompassGoals(GoalTag.Answer, GoalTag.Summarize)]
[CompassLane(Lane.Communicate)]
[CompassCost(0.3)]
[CompassRisk(0.1)]
public sealed class FakeModule { }

public class AttributeMetadataProviderTests
{
    [Fact]
    public void GetMetadata_ReadsAttributesFromRegisteredModuleType()
    {
        var provider = new AttributeMetadataProvider();
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);
        var proposal = new Proposal("test.proposal", [new ConstantValue(0.5)], _ => Task.CompletedTask);

        provider.RegisterModuleType("test.proposal", typeof(FakeModule));

        var meta = provider.GetMetadata(proposal, rt);

        Assert.NotNull(meta);
        Assert.Equal("test-domain", meta.Domain);
        Assert.Equal(Lane.Communicate, meta.Lane);
        Assert.Contains(GoalTag.Answer, meta.Goals);
        Assert.Contains(GoalTag.Summarize, meta.Goals);
        Assert.Equal(0.3, meta.EstimatedCost);
        Assert.Equal(0.1, meta.RiskLevel);
    }

    [Fact]
    public void GetMetadata_ReturnsExplicitRegistration_WhenBothAvailable()
    {
        var provider = new AttributeMetadataProvider();
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);
        var proposal = new Proposal("test.proposal", [new ConstantValue(0.5)], _ => Task.CompletedTask);

        var explicitMeta = new ProposalMetadata("explicit-domain", Lane.Execute, [GoalTag.Execute]);
        provider.Register("test.proposal", explicitMeta);
        provider.RegisterModuleType("test.proposal", typeof(FakeModule));

        var meta = provider.GetMetadata(proposal, rt);

        Assert.Equal("explicit-domain", meta!.Domain);
    }
}
