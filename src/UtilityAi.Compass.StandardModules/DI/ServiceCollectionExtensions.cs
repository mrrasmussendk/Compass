using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Capabilities;
using UtilityAi.Compass.Abstractions.Interfaces;

namespace UtilityAi.Compass.StandardModules.DI;

/// <summary>
/// Extension methods for registering the Compass standard modules with <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all built-in Compass standard modules and their workflow equivalents:
    /// <see cref="ConversationModule"/>, <see cref="FileReadModule"/>, <see cref="FileCreationModule"/>,
    /// <see cref="SummarizationModule"/>, <see cref="WebSearchModule"/>,
    /// <see cref="FileReadWorkflow"/>, <see cref="FileCreationWorkflow"/>,
    /// <see cref="SummarizationWorkflow"/>, and <see cref="WebSearchWorkflow"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// <see cref="ConversationModule"/>, <see cref="SummarizationModule"/> / <see cref="SummarizationWorkflow"/>, and
    /// <see cref="WebSearchModule"/> / <see cref="WebSearchWorkflow"/> require an
    /// <c>IModelClient</c> registration in the container.
    /// </remarks>
    public static IServiceCollection AddCompassStandardModules(
        this IServiceCollection services)
    {
        services.AddSingleton<ConversationModule>();
        services.AddSingleton<ICapabilityModule>(sp => sp.GetRequiredService<ConversationModule>());

        services.AddSingleton<FileReadModule>();
        services.AddSingleton<ICapabilityModule>(sp => sp.GetRequiredService<FileReadModule>());

        services.AddSingleton<FileCreationModule>();
        services.AddSingleton<ICapabilityModule>(sp => sp.GetRequiredService<FileCreationModule>());

        services.AddSingleton<SummarizationModule>();
        services.AddSingleton<ICapabilityModule>(sp => sp.GetRequiredService<SummarizationModule>());

        services.AddSingleton<WebSearchModule>();
        services.AddSingleton<ICapabilityModule>(sp => sp.GetRequiredService<WebSearchModule>());

        services.AddSingleton<CompoundRequestModule>();
        services.AddSingleton<ICapabilityModule>(sp => sp.GetRequiredService<CompoundRequestModule>());

        services.AddSingleton<IWorkflowModule, FileReadWorkflow>();
        services.AddSingleton<IWorkflowModule, FileCreationWorkflow>();
        services.AddSingleton<IWorkflowModule, SummarizationWorkflow>();
        services.AddSingleton<IWorkflowModule, WebSearchWorkflow>();

        return services;
    }
}
