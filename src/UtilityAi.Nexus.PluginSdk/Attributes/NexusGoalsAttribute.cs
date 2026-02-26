using UtilityAi.Nexus.Abstractions;

namespace UtilityAi.Nexus.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NexusGoalsAttribute : Attribute
{
    public GoalTag[] Goals { get; }
    public NexusGoalsAttribute(params GoalTag[] goals) { Goals = goals; }
}
