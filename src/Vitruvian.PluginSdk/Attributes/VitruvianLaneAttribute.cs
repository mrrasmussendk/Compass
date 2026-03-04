using VitruvianAbstractions;

namespace VitruvianPluginSdk.Attributes;

/// <summary>Assigns a capability module to a governance <see cref="Abstractions.Lane"/>.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class VitruvianLaneAttribute : Attribute
{
    /// <summary>Gets the governance lane this module belongs to.</summary>
    public Lane Lane { get; }

    /// <summary>Initializes a new instance of <see cref="VitruvianLaneAttribute"/>.</summary>
    /// <param name="lane">The <see cref="Abstractions.Lane"/> to assign.</param>
    public VitruvianLaneAttribute(Lane lane) { Lane = lane; }
}
