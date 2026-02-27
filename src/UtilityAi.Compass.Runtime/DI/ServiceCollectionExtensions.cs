using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Memory;
using UtilityAi.Compass.Abstractions.CliAction;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Runtime.Modules;
using UtilityAi.Compass.Runtime.Sensors;
using UtilityAi.Compass.Runtime.Strategy;
using UtilityAi.Orchestration;

namespace UtilityAi.Compass.Runtime.DI;

/// <summary>
/// Extension methods for registering Compass runtime services with <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Compass runtime sensors, modules, and the governed selection strategy.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional callback to customise <see cref="CompassOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddUtilityAiCompass(
        this IServiceCollection services,
        Action<CompassOptions>? configure = null)
    {
        var options = new CompassOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton(options.GovernanceConfig);
        services.AddSingleton<IMemoryStore, InMemoryStore>();

        services.AddSingleton<CorrelationSensor>();
        services.AddSingleton<GoalRouterSensor>();
        services.AddSingleton<LaneRouterSensor>();
        services.AddSingleton<CliIntentSensor>();
        services.AddSingleton<GovernanceMemoryProjectionSensor>(sp =>
            new GovernanceMemoryProjectionSensor(
                sp.GetRequiredService<IMemoryStore>(),
                options.TrackedCooldownKeys));

        services.AddSingleton<WorkflowStateSensor>(sp =>
            new WorkflowStateSensor(sp.GetRequiredService<IMemoryStore>()));
        services.AddSingleton<ValidationStateSensor>(sp =>
            new ValidationStateSensor(sp.GetRequiredService<IMemoryStore>()));

        services.AddSingleton<RoutingBootstrapModule>();
        services.AddSingleton<CliActionModule>(sp =>
            new CliActionModule(sp.GetServices<ICliAction>()));

        if (options.EnableGovernanceFinalizer)
            services.AddSingleton<GovernanceFinalizerModule>();

        services.AddSingleton<CompassGovernedSelectionStrategy>(sp =>
            new CompassGovernedSelectionStrategy(
                sp.GetRequiredService<IMemoryStore>(),
                sp.GetRequiredService<IProposalMetadataProvider>(),
                options.GovernanceConfig));

        services.AddSingleton<ISelectionStrategy>(sp =>
            sp.GetRequiredService<CompassGovernedSelectionStrategy>());

        return services;
    }
}
