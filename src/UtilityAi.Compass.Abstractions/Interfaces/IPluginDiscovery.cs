using UtilityAi.Capabilities;
using UtilityAi.Compass.Abstractions.CliAction;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.Abstractions.Interfaces;

public interface IPluginDiscovery
{
    IEnumerable<ICapabilityModule> DiscoverModules();
    IEnumerable<ISensor> DiscoverSensors();
    IEnumerable<IOrchestrationSink> DiscoverSinks();
    IEnumerable<ICliAction> DiscoverCliActions() => Enumerable.Empty<ICliAction>();
}
