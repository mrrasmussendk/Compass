using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Utils;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.PluginSdk.MetadataProvider;
using UtilityAi.Compass.Runtime.Strategy;
using UtilityAi.Memory;

namespace UtilityAi.Compass.Tests;

public class CompassGovernedSelectionStrategyTests
{
    private static CompassGovernedSelectionStrategy CreateStrategy(AttributeMetadataProvider? provider = null)
    {
        var store = new InMemoryStore();
        var metaProvider = provider ?? new AttributeMetadataProvider();
        return new CompassGovernedSelectionStrategy(store, metaProvider);
    }

    [Fact]
    public void Select_ReturnsHighestUtility_WhenNoMetadata()
    {
        var strategy = CreateStrategy();
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var p1 = new Proposal("p1", [new ConstantValue(0.3)], _ => Task.CompletedTask);
        var p2 = new Proposal("p2", [new ConstantValue(0.8)], _ => Task.CompletedTask);

        var scored = new List<(Proposal P, double Utility)>
        {
            (p1, 0.3),
            (p2, 0.8)
        };

        var result = strategy.Select(scored, rt);
        Assert.Equal("p2", result.Id);
    }

    [Fact]
    public void Select_FiltersToGoalMatchingProposals()
    {
        var provider = new AttributeMetadataProvider();
        provider.Register("p1", new ProposalMetadata("d", Lane.Communicate, [GoalTag.Answer]));
        provider.Register("p2", new ProposalMetadata("d", Lane.Execute, [GoalTag.Execute]));

        var strategy = CreateStrategy(provider);
        var bus = new EventBus();
        bus.Publish(new GoalSelected(GoalTag.Answer, 0.9));
        bus.Publish(new LaneSelected(Lane.Communicate));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var p1 = new Proposal("p1", [new ConstantValue(0.5)], _ => Task.CompletedTask);
        var p2 = new Proposal("p2", [new ConstantValue(0.9)], _ => Task.CompletedTask);

        var scored = new List<(Proposal P, double Utility)> { (p1, 0.5), (p2, 0.9) };

        var result = strategy.Select(scored, rt);
        Assert.Equal("p1", result.Id);
    }

    [Fact]
    public void Select_ThrowsOnEmptyList()
    {
        var strategy = CreateStrategy();
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        Assert.Throws<InvalidOperationException>(() =>
            strategy.Select(Array.Empty<(Proposal P, double Utility)>(), rt));
    }
}
