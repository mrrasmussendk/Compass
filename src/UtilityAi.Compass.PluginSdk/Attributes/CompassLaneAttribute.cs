using UtilityAi.Compass.Abstractions;

namespace UtilityAi.Compass.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassLaneAttribute : Attribute
{
    public Lane Lane { get; }
    public CompassLaneAttribute(Lane lane) { Lane = lane; }
}
