using UtilityAi.Nexus.Abstractions;

namespace UtilityAi.Nexus.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NexusConflictsAttribute : Attribute
{
    public string[]? Ids { get; }
    public GoalTag[]? Tags { get; }
    public NexusConflictsAttribute(string[]? ids = null, GoalTag[]? tags = null) { Ids = ids; Tags = tags; }
}
