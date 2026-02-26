using UtilityAi.Compass.Abstractions;

namespace UtilityAi.Compass.PluginSdk.Attributes;

/// <summary>Declares conflict rules that prevent a module from running alongside conflicting proposals.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassConflictsAttribute : Attribute
{
    /// <summary>Gets the proposal identifiers that conflict with this module, or <c>null</c> if none.</summary>
    public string[]? Ids { get; }

    /// <summary>Gets the <see cref="GoalTag"/> values that conflict with this module, or <c>null</c> if none.</summary>
    public GoalTag[]? Tags { get; }

    /// <summary>Initializes a new instance of <see cref="CompassConflictsAttribute"/>.</summary>
    /// <param name="ids">Optional array of conflicting proposal identifiers.</param>
    /// <param name="tags">Optional array of conflicting <see cref="GoalTag"/> values.</param>
    public CompassConflictsAttribute(string[]? ids = null, GoalTag[]? tags = null) { Ids = ids; Tags = tags; }
}
