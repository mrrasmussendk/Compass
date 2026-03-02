using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Capabilities;
using UtilityAi.Memory;
using UtilityAi.Compass.Abstractions.CliAction;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Runtime.Memory;
using UtilityAi.Compass.Runtime.Modules;
using UtilityAi.Compass.Runtime.Sensors;
using UtilityAi.Compass.Runtime.Strategy;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;

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
        services.AddSingleton<IMemoryStore>(_ =>
        {
            var connectionString = string.IsNullOrWhiteSpace(options.MemoryConnectionString)
                ? $"Data Source={Path.Combine(AppContext.BaseDirectory, "appdb", "compass-memory.db")}"
                : options.MemoryConnectionString;
            return new SqliteMemoryStore(connectionString);
        });

        services.AddSingleton<CorrelationSensor>();
        services.AddSingleton<ISensor>(sp => sp.GetRequiredService<CorrelationSensor>());

        services.AddSingleton<GoalRouterSensor>(sp =>
        {
            var modelClient = sp.GetService<IModelClient>();
            var memoryStore = sp.GetRequiredService<IMemoryStore>();
            return modelClient is not null 
                ? new GoalRouterSensor(modelClient, memoryStore)
                : new GoalRouterSensor();
        });
        services.AddSingleton<ISensor>(sp => sp.GetRequiredService<GoalRouterSensor>());

        services.AddSingleton<CliIntentSensor>();
        services.AddSingleton<ISensor>(sp => sp.GetRequiredService<CliIntentSensor>());

        services.AddSingleton<GovernanceMemoryProjectionSensor>(sp =>
            new GovernanceMemoryProjectionSensor(
                sp.GetRequiredService<IMemoryStore>(),
                sp.GetRequiredService<IProposalMetadataProvider>(),
                options.TrackedCooldownKeys));
        services.AddSingleton<ISensor>(sp => sp.GetRequiredService<GovernanceMemoryProjectionSensor>());

        services.AddSingleton<WorkflowStateSensor>(sp =>
            new WorkflowStateSensor(sp.GetRequiredService<IMemoryStore>()));
        services.AddSingleton<ISensor>(sp => sp.GetRequiredService<WorkflowStateSensor>());

        services.AddSingleton<ValidationStateSensor>(sp =>
            new ValidationStateSensor(sp.GetRequiredService<IMemoryStore>()));
        services.AddSingleton<ISensor>(sp => sp.GetRequiredService<ValidationStateSensor>());

        services.AddSingleton<RoutingBootstrapModule>();
        services.AddSingleton<ICapabilityModule>(sp => sp.GetRequiredService<RoutingBootstrapModule>());

        services.AddSingleton<CliActionModule>(sp =>
            new CliActionModule(sp.GetServices<ICliAction>()));
        services.AddSingleton<ICapabilityModule>(sp => sp.GetRequiredService<CliActionModule>());

        services.AddSingleton<WorkflowOrchestratorModule>(sp =>
            new WorkflowOrchestratorModule(sp.GetServices<IWorkflowModule>()));
        services.AddSingleton<ICapabilityModule>(sp => sp.GetRequiredService<WorkflowOrchestratorModule>());

        if (options.EnableGovernanceFinalizer)
        {
            services.AddSingleton<GovernanceFinalizerModule>();
            services.AddSingleton<ICapabilityModule>(sp => sp.GetRequiredService<GovernanceFinalizerModule>());
        }

        if (options.EnableScheduler)
        {
            services.AddSingleton<SchedulerSensor>(sp =>
                new SchedulerSensor(sp.GetRequiredService<IMemoryStore>()));
            services.AddSingleton<ISensor>(sp => sp.GetRequiredService<SchedulerSensor>());

            services.AddSingleton<SchedulerModule>(sp =>
                new SchedulerModule(sp.GetRequiredService<IMemoryStore>()));
            services.AddSingleton<ICapabilityModule>(sp => sp.GetRequiredService<SchedulerModule>());

            services.AddSingleton<SchedulerService>(sp =>
                new SchedulerService(
                    sp.GetRequiredService<IMemoryStore>(),
                    options.SchedulerPollInterval));
        }

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
