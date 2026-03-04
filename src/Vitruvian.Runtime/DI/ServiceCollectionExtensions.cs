using Microsoft.Extensions.DependencyInjection;

namespace VitruvianRuntime.DI;

/// <summary>
/// Extension methods for registering Vitruvian runtime services with <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers core Vitruvian runtime services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional callback to customize <see cref="VitruvianOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddUtilityAiVitruvian(
        this IServiceCollection services,
        Action<VitruvianOptions>? configure = null)
    {
        var options = new VitruvianOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        return services;
    }
}
