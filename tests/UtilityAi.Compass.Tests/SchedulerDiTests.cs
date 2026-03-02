using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Compass.Runtime.DI;
using UtilityAi.Compass.Runtime.Modules;
using UtilityAi.Compass.Runtime.Sensors;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.Tests;

public class SchedulerDiTests
{
    [Fact]
    public void AddUtilityAiCompass_WithSchedulerEnabled_RegistersSchedulerComponents()
    {
        var services = new ServiceCollection();
        services.AddUtilityAiCompass(opts => opts.EnableScheduler = true);
        using var provider = services.BuildServiceProvider();

        var sensor = provider.GetService<SchedulerSensor>();
        Assert.NotNull(sensor);

        var module = provider.GetService<SchedulerModule>();
        Assert.NotNull(module);

        var service = provider.GetService<SchedulerService>();
        Assert.NotNull(service);
    }

    [Fact]
    public void AddUtilityAiCompass_WithSchedulerDisabled_DoesNotRegisterSchedulerComponents()
    {
        var services = new ServiceCollection();
        services.AddUtilityAiCompass(opts => opts.EnableScheduler = false);
        using var provider = services.BuildServiceProvider();

        var sensor = provider.GetService<SchedulerSensor>();
        Assert.Null(sensor);

        var module = provider.GetService<SchedulerModule>();
        Assert.Null(module);

        var service = provider.GetService<SchedulerService>();
        Assert.Null(service);
    }
}
