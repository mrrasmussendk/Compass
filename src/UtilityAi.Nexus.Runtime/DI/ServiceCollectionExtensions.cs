using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Memory;
using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Nexus.Abstractions.Interfaces;
using UtilityAi.Nexus.Runtime.Modules;
using UtilityAi.Nexus.Runtime.Sensors;
using UtilityAi.Nexus.Runtime.Strategy;
using UtilityAi.Orchestration;

namespace UtilityAi.Nexus.Runtime.DI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUtilityAiNexus(
        this IServiceCollection services,
        Action<NexusOptions>? configure = null)
    {
        var options = new NexusOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton(options.GovernanceConfig);
        services.AddSingleton<IMemoryStore, InMemoryStore>();

        services.AddSingleton<CorrelationSensor>();
        services.AddSingleton<GoalRouterSensor>();
        services.AddSingleton<LaneRouterSensor>();
        services.AddSingleton<GovernanceMemoryProjectionSensor>(sp =>
            new GovernanceMemoryProjectionSensor(
                sp.GetRequiredService<IMemoryStore>(),
                options.TrackedCooldownKeys));

        services.AddSingleton<RoutingBootstrapModule>();

        if (options.EnableGovernanceFinalizer)
            services.AddSingleton<GovernanceFinalizerModule>();

        services.AddSingleton<NexusGovernedSelectionStrategy>(sp =>
            new NexusGovernedSelectionStrategy(
                sp.GetRequiredService<IMemoryStore>(),
                sp.GetRequiredService<IProposalMetadataProvider>(),
                options.GovernanceConfig));

        services.AddSingleton<ISelectionStrategy>(sp =>
            sp.GetRequiredService<NexusGovernedSelectionStrategy>());

        return services;
    }
}
