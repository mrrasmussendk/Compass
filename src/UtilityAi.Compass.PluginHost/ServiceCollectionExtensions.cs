using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Capabilities;
using UtilityAi.Compass.Abstractions.CliAction;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.PluginHost;

/// <summary>Dependency-injection extensions for loading Compass plugins.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Loads all plugin assemblies from <paramref name="folderPath"/> and registers
    /// discovered modules, sensors, sinks, and CLI actions with the service collection.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="folderPath">Path to the folder containing plugin DLLs.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
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
        {
            services.AddSingleton(type);
            services.AddSingleton(typeof(ICapabilityModule), sp => sp.GetRequiredService(type));
        }

        foreach (var type in loader.DiscoverTypes<ISensor>())
        {
            services.AddSingleton(type);
            services.AddSingleton(typeof(ISensor), sp => sp.GetRequiredService(type));
        }

        foreach (var type in loader.DiscoverTypes<IOrchestrationSink>())
            services.AddSingleton(type);

        foreach (var type in loader.DiscoverTypes<ICliAction>())
            services.AddSingleton(typeof(ICliAction), type);

        foreach (var type in loader.DiscoverTypes<IWorkflowModule>())
            services.AddSingleton(typeof(IWorkflowModule), type);

        return services;
    }
}
