using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Nexus.SamplePlugin.Basic;

public sealed class BasicSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        rt.Bus.Publish(new UserRequest("Hello from BasicSensor", "system"));
        return Task.CompletedTask;
    }
}
