using Microsoft.Extensions.DependencyInjection;

namespace UtilityAi.Compass.StandardModules.DI;

/// <summary>
/// Extension methods for registering the Compass standard modules with <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all built-in Compass standard modules:
    /// <see cref="FileReadModule"/>, <see cref="FileCreationModule"/>,
    /// <see cref="SummarizationModule"/>, and <see cref="WebSearchModule"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// <see cref="SummarizationModule"/> and <see cref="WebSearchModule"/> require an
    /// <c>IModelClient</c> registration in the container.
    /// </remarks>
    public static IServiceCollection AddCompassStandardModules(
        this IServiceCollection services)
    {
        services.AddSingleton<FileReadModule>();
        services.AddSingleton<FileCreationModule>();
        services.AddSingleton<SummarizationModule>();
        services.AddSingleton<WebSearchModule>();

        return services;
    }
}
