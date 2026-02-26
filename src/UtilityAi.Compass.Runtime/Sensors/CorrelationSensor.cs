using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.Runtime.Sensors;

/// <summary>
/// Assigns a unique <see cref="CorrelationId"/> fact to each tick if one does not already exist.
/// </summary>
public sealed class CorrelationSensor : ISensor
{
    /// <inheritdoc />
    public Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        if (!rt.Bus.TryGet<CorrelationId>(out _))
            rt.Bus.Publish(new CorrelationId(Guid.NewGuid().ToString("N")));
        return Task.CompletedTask;
    }
}
