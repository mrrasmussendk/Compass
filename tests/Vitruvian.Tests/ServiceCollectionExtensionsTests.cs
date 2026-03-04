using Microsoft.Extensions.DependencyInjection;
using VitruvianAbstractions.Scheduling;
using VitruvianRuntime.DI;
using VitruvianRuntime.Scheduling;
using Xunit;

namespace VitruvianTests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddUtilityAiVitruvian_SchedulerEnabled_RegistersSchedulerServices()
    {
        var services = new ServiceCollection();

        services.AddUtilityAiVitruvian(opts => opts.EnableScheduler = true);

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IScheduledTaskStore>());
        Assert.NotNull(provider.GetService<NaturalLanguageScheduleParser>());
    }

    [Fact]
    public void AddUtilityAiVitruvian_SchedulerDisabled_DoesNotRegisterSchedulerServices()
    {
        var services = new ServiceCollection();

        services.AddUtilityAiVitruvian(opts => opts.EnableScheduler = false);

        var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IScheduledTaskStore>());
        Assert.Null(provider.GetService<NaturalLanguageScheduleParser>());
    }
}
