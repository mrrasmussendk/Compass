using UtilityAi.Nexus.Abstractions;

namespace UtilityAi.Nexus.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NexusLaneAttribute : Attribute
{
    public Lane Lane { get; }
    public NexusLaneAttribute(Lane lane) { Lane = lane; }
}
