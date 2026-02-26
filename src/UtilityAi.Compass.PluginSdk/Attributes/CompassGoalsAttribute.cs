using UtilityAi.Compass.Abstractions;

namespace UtilityAi.Compass.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassGoalsAttribute : Attribute
{
    public GoalTag[] Goals { get; }
    public CompassGoalsAttribute(params GoalTag[] goals) { Goals = goals; }
}
