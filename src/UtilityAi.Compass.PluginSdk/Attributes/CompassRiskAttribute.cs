namespace UtilityAi.Compass.PluginSdk.Attributes;

/// <summary>Declares the risk level of invoking a module's proposals.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassRiskAttribute : Attribute
{
    /// <summary>Gets the risk level (0.0 = no risk, 1.0 = maximum risk).</summary>
    public double Risk { get; }

    /// <summary>Initializes a new instance of <see cref="CompassRiskAttribute"/>.</summary>
    /// <param name="risk">The risk level associated with this module's proposals.</param>
    public CompassRiskAttribute(double risk) { Risk = risk; }
}
