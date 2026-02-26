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

public static class ServiceCollectionExtensions
{
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
