using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.PluginSdk.MetadataProvider;
using UtilityAi.Compass.Runtime.Sensors;
using UtilityAi.Memory;
using UtilityAi.Utils;
using Rt = UtilityAi.Utils.Runtime;

namespace UtilityAi.Compass.Tests;

public class GovernanceMemoryProjectionSensorTests
{
    [Fact]
    public async Task SenseAsync_UsesMetadataCooldownTtl_ForTrackedKey()
    {
        var store = new InMemoryStore();
        var metadataProvider = new AttributeMetadataProvider();
        metadataProvider.Register("proposal.short-ttl",
            new ProposalMetadata("test", Lane.Execute, [GoalTag.Execute], CooldownKeyTemplate: "proposal.short-ttl", CooldownTtl: TimeSpan.FromSeconds(5)));

        await store.StoreAsync(
            new ProposalExecutionRecord("proposal.short-ttl", null, DateTimeOffset.UtcNow.AddSeconds(-7), 0.9),
            DateTimeOffset.UtcNow);

        var sensor = new GovernanceMemoryProjectionSensor(store, metadataProvider, ["proposal.short-ttl"]);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var cooldown = bus.GetOrDefault<CooldownState>();
        Assert.NotNull(cooldown);
        Assert.Equal("proposal.short-ttl", cooldown.Key);
        Assert.False(cooldown.IsActive);
    }

    [Fact]
    public async Task SenseAsync_FallsBackToDefaultCooldownTtl_WhenMetadataMissing()
    {
        var store = new InMemoryStore();
        var metadataProvider = new AttributeMetadataProvider();

        await store.StoreAsync(
            new ProposalExecutionRecord("proposal.unknown", null, DateTimeOffset.UtcNow.AddSeconds(-7), 0.9),
            DateTimeOffset.UtcNow);

        var sensor = new GovernanceMemoryProjectionSensor(store, metadataProvider, ["proposal.unknown"]);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var cooldown = bus.GetOrDefault<CooldownState>();
        Assert.NotNull(cooldown);
        Assert.Equal("proposal.unknown", cooldown.Key);
        Assert.True(cooldown.IsActive);
        Assert.NotNull(cooldown.Remaining);
    }
}
