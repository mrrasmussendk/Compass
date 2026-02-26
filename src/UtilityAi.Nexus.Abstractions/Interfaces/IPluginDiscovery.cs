using UtilityAi.Capabilities;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;

namespace UtilityAi.Nexus.Abstractions.Interfaces;

public interface IPluginDiscovery
{
    IEnumerable<ICapabilityModule> DiscoverModules();
    IEnumerable<ISensor> DiscoverSensors();
    IEnumerable<IOrchestrationSink> DiscoverSinks();
}
