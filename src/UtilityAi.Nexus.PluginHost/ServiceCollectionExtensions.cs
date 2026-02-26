using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Nexus.Abstractions.Interfaces;

namespace UtilityAi.Nexus.PluginHost;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNexusPluginsFromFolder(
        this IServiceCollection services,
        string folderPath)
    {
        var loader = new PluginLoader();
        loader.LoadFromFolder(folderPath);

        services.AddSingleton<IPluginDiscovery>(loader);

        foreach (var module in loader.DiscoverModules())
            services.AddSingleton(module.GetType(), module);

        foreach (var sensor in loader.DiscoverSensors())
            services.AddSingleton(sensor.GetType(), sensor);

        foreach (var sink in loader.DiscoverSinks())
            services.AddSingleton(sink.GetType(), sink);

        return services;
    }
}
