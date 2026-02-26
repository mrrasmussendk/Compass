using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Compass.SamplePlugin.Basic;

public sealed class BasicSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        rt.Bus.Publish(new UserRequest("Hello from BasicSensor", "system"));
        return Task.CompletedTask;
    }
}
