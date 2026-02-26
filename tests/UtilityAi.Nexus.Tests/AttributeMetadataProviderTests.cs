using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Nexus.Abstractions;
using UtilityAi.Utils;
using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Nexus.PluginSdk.Attributes;
using UtilityAi.Nexus.PluginSdk.MetadataProvider;

namespace UtilityAi.Nexus.Tests;

[NexusCapability("test-domain", priority: 5)]
[NexusGoals(GoalTag.Answer, GoalTag.Summarize)]
[NexusLane(Lane.Communicate)]
[NexusSideEffects(SideEffectLevel.Write)]
[NexusCost(0.3)]
[NexusRisk(0.1)]
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
        Assert.Equal(SideEffectLevel.Write, meta.SideEffects);
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
