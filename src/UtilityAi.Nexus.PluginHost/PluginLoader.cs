using System.Reflection;
using UtilityAi.Capabilities;
using UtilityAi.Nexus.Abstractions.Interfaces;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;

namespace UtilityAi.Nexus.PluginHost;

public sealed class PluginLoader : IPluginDiscovery
{
    private readonly List<Assembly> _assemblies = new();

    public PluginLoader() { }

    public void LoadFromFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        foreach (var dll in Directory.GetFiles(folderPath, "*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dll);
                _assemblies.Add(assembly);
            }
            catch { /* skip invalid assemblies */ }
        }
    }

    public void LoadAssembly(Assembly assembly)
    {
        _assemblies.Add(assembly);
    }

    public IEnumerable<ICapabilityModule> DiscoverModules()
    {
        return DiscoverImplementations<ICapabilityModule>();
    }

    public IEnumerable<ISensor> DiscoverSensors()
    {
        return DiscoverImplementations<ISensor>();
    }

    public IEnumerable<IOrchestrationSink> DiscoverSinks()
    {
        return DiscoverImplementations<IOrchestrationSink>();
    }

    public IEnumerable<PluginManifest> GetManifests()
    {
        foreach (var assembly in _assemblies)
        {
            var modules = GetConcreteTypes<ICapabilityModule>(assembly).Select(t => t.FullName ?? t.Name).ToList();
            var sensors = GetConcreteTypes<ISensor>(assembly).Select(t => t.FullName ?? t.Name).ToList();
            var sinks = GetConcreteTypes<IOrchestrationSink>(assembly).Select(t => t.FullName ?? t.Name).ToList();

            yield return new PluginManifest(
                AssemblyName: assembly.GetName().Name ?? assembly.FullName ?? "unknown",
                Version: assembly.GetName().Version?.ToString(),
                ModuleTypes: modules,
                SensorTypes: sensors,
                SinkTypes: sinks
            );
        }
    }

    private IEnumerable<T> DiscoverImplementations<T>()
    {
        foreach (var assembly in _assemblies)
        {
            foreach (var type in GetConcreteTypes<T>(assembly))
            {
                T? instance = default;
                try
                {
                    instance = (T?)Activator.CreateInstance(type);
                }
                catch { /* skip types that can't be default-constructed */ }

                if (instance is not null)
                    yield return instance;
            }
        }
    }

    private static IEnumerable<Type> GetConcreteTypes<T>(Assembly assembly)
    {
        var interfaceType = typeof(T);
        try
        {
            return assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t));
        }
        catch { return Enumerable.Empty<Type>(); }
    }
}
