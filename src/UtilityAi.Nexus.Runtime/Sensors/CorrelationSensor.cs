using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Sensor;

namespace UtilityAi.Nexus.Runtime.Sensors;

public sealed class CorrelationSensor : ISensor
{
    public Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        if (!rt.Bus.TryGet<CorrelationId>(out _))
            rt.Bus.Publish(new CorrelationId(Guid.NewGuid().ToString("N")));
        return Task.CompletedTask;
    }
}
