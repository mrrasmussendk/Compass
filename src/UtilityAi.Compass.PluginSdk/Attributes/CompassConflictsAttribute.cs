using UtilityAi.Compass.Abstractions;

namespace UtilityAi.Compass.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassConflictsAttribute : Attribute
{
    public string[]? Ids { get; }
    public GoalTag[]? Tags { get; }
    public CompassConflictsAttribute(string[]? ids = null, GoalTag[]? tags = null) { Ids = ids; Tags = tags; }
}
