using UtilityAi.Compass.Abstractions;

namespace UtilityAi.Compass.PluginSdk.Attributes;

/// <summary>Declares which <see cref="GoalTag"/> values a capability module can serve.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassGoalsAttribute : Attribute
{
    /// <summary>Gets the set of goal tags this module is eligible for.</summary>
    public GoalTag[] Goals { get; }

    /// <summary>Initializes a new instance of <see cref="CompassGoalsAttribute"/>.</summary>
    /// <param name="goals">One or more <see cref="GoalTag"/> values the module can serve.</param>
    public CompassGoalsAttribute(params GoalTag[] goals) { Goals = goals; }
}
