using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Utils;
using UtilityAi.Nexus.Runtime.Sensors;

namespace UtilityAi.Nexus.Tests;

public class CorrelationSensorTests
{
    [Fact]
    public async Task SenseAsync_PublishesCorrelationId_WhenNotPresent()
    {
        var sensor = new CorrelationSensor();
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var corrId = bus.GetOrDefault<CorrelationId>();
        Assert.NotNull(corrId);
        Assert.NotEmpty(corrId.Value);
    }

    [Fact]
    public async Task SenseAsync_DoesNotOverwrite_WhenPresent()
    {
        var sensor = new CorrelationSensor();
        var bus = new EventBus();
        bus.Publish(new CorrelationId("existing-id"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var corrId = bus.GetOrDefault<CorrelationId>();
        Assert.Equal("existing-id", corrId!.Value);
    }
}
