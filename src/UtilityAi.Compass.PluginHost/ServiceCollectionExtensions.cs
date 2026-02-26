using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Capabilities;
using UtilityAi.Compass.Abstractions.CliAction;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.PluginHost;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCompassPluginsFromFolder(
        this IServiceCollection services,
        string folderPath)
    {
        var loader = new PluginLoader();
        loader.LoadFromFolder(folderPath);

        services.AddSingleton<IPluginDiscovery>(loader);

        // Register plugin types for DI-based construction so the container
        // can inject framework services (e.g. IModelClient) into modules.
        foreach (var type in loader.DiscoverTypes<ICapabilityModule>())
            services.AddSingleton(type);

        foreach (var type in loader.DiscoverTypes<ISensor>())
            services.AddSingleton(type);

        foreach (var type in loader.DiscoverTypes<IOrchestrationSink>())
            services.AddSingleton(type);

        foreach (var type in loader.DiscoverTypes<ICliAction>())
            services.AddSingleton(typeof(ICliAction), type);

        return services;
    }
}
