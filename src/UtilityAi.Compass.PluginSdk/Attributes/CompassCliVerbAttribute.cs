using UtilityAi.Compass.Abstractions;

namespace UtilityAi.Compass.PluginSdk.Attributes;

/// <summary>
/// Marks an <see cref="Abstractions.CliAction.ICliAction"/> implementation
/// with its CLI verb and route for discovery and metadata extraction.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassCliVerbAttribute : Attribute
{
    public CliVerb Verb { get; }
    public string Route { get; }

    public CompassCliVerbAttribute(CliVerb verb, string route)
    {
        Verb = verb;
        Route = route;
    }
}
