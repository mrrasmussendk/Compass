using System.Reflection;
using UtilityAi.Capabilities;
using UtilityAi.Compass.Abstractions.CliAction;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.PluginHost;

/// <summary>
/// Loads plugin assemblies from disk and discovers
/// <see cref="ICapabilityModule"/>, <see cref="ISensor"/>,
/// <see cref="IOrchestrationSink"/>, and <see cref="ICliAction"/> types.
/// </summary>
public sealed class PluginLoader : IPluginDiscovery
{
    private readonly List<Assembly> _assemblies = new();

    /// <summary>Initializes a new instance of <see cref="PluginLoader"/>.</summary>
    public PluginLoader() { }

    /// <summary>Loads all <c>.dll</c> files from the specified folder as plugin assemblies.</summary>
    /// <param name="folderPath">Path to the folder containing plugin DLLs.</param>
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
            catch (BadImageFormatException) { /* skip invalid assemblies */ }
            catch (FileLoadException) { /* skip load failures for optional plugins */ }
        }
    }

    /// <summary>Adds an already-loaded assembly to the plugin discovery set.</summary>
    /// <param name="assembly">The assembly to include in discovery.</param>
    public void LoadAssembly(Assembly assembly)
    {
        _assemblies.Add(assembly);
    }

    /// <summary>Discovers and instantiates all <see cref="ICapabilityModule"/> implementations from loaded assemblies.</summary>
    public IEnumerable<ICapabilityModule> DiscoverModules()
    {
        return DiscoverImplementations<ICapabilityModule>();
    }

    /// <summary>Discovers and instantiates all <see cref="ISensor"/> implementations from loaded assemblies.</summary>
    public IEnumerable<ISensor> DiscoverSensors()
    {
        return DiscoverImplementations<ISensor>();
    }

    /// <summary>Discovers and instantiates all <see cref="IOrchestrationSink"/> implementations from loaded assemblies.</summary>
    public IEnumerable<IOrchestrationSink> DiscoverSinks()
    {
        return DiscoverImplementations<IOrchestrationSink>();
    }

    /// <summary>Discovers and instantiates all <see cref="ICliAction"/> implementations from loaded assemblies.</summary>
    public IEnumerable<ICliAction> DiscoverCliActions()
    {
        return DiscoverImplementations<ICliAction>();
    }

    /// <summary>Discovers and instantiates all <see cref="IWorkflowModule"/> implementations from loaded assemblies.</summary>
    public IEnumerable<IWorkflowModule> DiscoverWorkflowModules()
    {
        return DiscoverImplementations<IWorkflowModule>();
    }

    /// <summary>
    /// Returns concrete types that implement <typeparamref name="T"/> across
    /// all loaded assemblies, without instantiating them. This enables the DI
    /// container to construct them with proper dependency injection.
    /// </summary>
    public IEnumerable<Type> DiscoverTypes<T>()
    {
        foreach (var assembly in _assemblies)
        {
            foreach (var type in GetConcreteTypes<T>(assembly))
                yield return type;
        }
    }

    /// <summary>Returns a <see cref="PluginManifest"/> for each loaded assembly describing its discovered types.</summary>
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
                catch (MissingMethodException) { /* skip types that can't be default-constructed */ }
                catch (MemberAccessException) { /* skip inaccessible constructors */ }
                catch (TargetInvocationException) { /* skip constructors that throw */ }

                if (instance is not null)
                    yield return instance;
            }
        }
    }

    private static IEnumerable<Type> GetConcreteTypes<T>(Assembly assembly)
    {
        var interfaceType = typeof(T);
        bool Matches(Type t) => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t);
        try
        {
            return assembly.GetTypes()
                .Where(Matches);
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null && Matches(t)).Cast<Type>();
        }
    }
}
