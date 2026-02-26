using UtilityAi.Compass.Abstractions;

namespace UtilityAi.Compass.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassSideEffectsAttribute : Attribute
{
    public SideEffectLevel Level { get; }
    public CompassSideEffectsAttribute(SideEffectLevel level) { Level = level; }
}
