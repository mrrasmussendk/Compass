using UtilityAi.Compass.Abstractions;

namespace UtilityAi.Compass.PluginSdk.Attributes;

/// <summary>Declares the side-effect level of a capability module.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassSideEffectsAttribute : Attribute
{
    /// <summary>Gets the <see cref="SideEffectLevel"/> for this module.</summary>
    public SideEffectLevel Level { get; }

    /// <summary>Initializes a new instance of <see cref="CompassSideEffectsAttribute"/>.</summary>
    /// <param name="level">The side-effect level to declare.</param>
    public CompassSideEffectsAttribute(SideEffectLevel level) { Level = level; }
}
