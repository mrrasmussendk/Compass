using VitruvianAbstractions;

namespace VitruvianPluginSdk.Attributes;

/// <summary>Declares the side-effect level of a capability module.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class VitruvianSideEffectsAttribute : Attribute
{
    /// <summary>Gets the <see cref="SideEffectLevel"/> for this module.</summary>
    public SideEffectLevel Level { get; }

    /// <summary>Initializes a new instance of <see cref="VitruvianSideEffectsAttribute"/>.</summary>
    /// <param name="level">The side-effect level to declare.</param>
    public VitruvianSideEffectsAttribute(SideEffectLevel level) { Level = level; }
}
