using UtilityAi.Nexus.Abstractions;

namespace UtilityAi.Nexus.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NexusSideEffectsAttribute : Attribute
{
    public SideEffectLevel Level { get; }
    public NexusSideEffectsAttribute(SideEffectLevel level) { Level = level; }
}
