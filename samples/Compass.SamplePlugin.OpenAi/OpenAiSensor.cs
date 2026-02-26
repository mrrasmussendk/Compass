using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Compass.SamplePlugin.OpenAi;

public sealed class OpenAiSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        rt.Bus.Publish(new UserRequest("Hello from OpenAiSensor", "system"));
        return Task.CompletedTask;
    }
}
