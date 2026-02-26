using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Compass.Abstractions.CliAction;
using UtilityAi.Compass.Abstractions.Interfaces;

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

        foreach (var module in loader.DiscoverModules())
            services.AddSingleton(module.GetType(), module);

        foreach (var sensor in loader.DiscoverSensors())
            services.AddSingleton(sensor.GetType(), sensor);

        foreach (var sink in loader.DiscoverSinks())
            services.AddSingleton(sink.GetType(), sink);

        foreach (var cliAction in loader.DiscoverCliActions())
            services.AddSingleton<ICliAction>(cliAction);

        return services;
    }
}
