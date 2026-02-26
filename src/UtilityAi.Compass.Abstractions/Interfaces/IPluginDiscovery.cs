using UtilityAi.Capabilities;
using UtilityAi.Compass.Abstractions.CliAction;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.Abstractions.Interfaces;

/// <summary>
/// Discovers capability modules, sensors, sinks, and CLI actions from a plugin assembly.
/// Implemented by the plugin host to scan loaded assemblies for Compass components.
/// </summary>
public interface IPluginDiscovery
{
    /// <summary>Discovers all <see cref="ICapabilityModule"/> implementations provided by plugins.</summary>
    IEnumerable<ICapabilityModule> DiscoverModules();

    /// <summary>Discovers all <see cref="ISensor"/> implementations provided by plugins.</summary>
    IEnumerable<ISensor> DiscoverSensors();

    /// <summary>Discovers all <see cref="IOrchestrationSink"/> implementations provided by plugins.</summary>
    IEnumerable<IOrchestrationSink> DiscoverSinks();

    /// <summary>Discovers all <see cref="ICliAction"/> implementations provided by plugins.</summary>
    IEnumerable<ICliAction> DiscoverCliActions() => Enumerable.Empty<ICliAction>();
}
