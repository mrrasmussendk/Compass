namespace UtilityAi.Compass.PluginSdk.Attributes;

/// <summary>Declares the estimated cost of invoking a module's proposals.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassCostAttribute : Attribute
{
    /// <summary>Gets the estimated cost value (0.0 = free, 1.0 = maximum).</summary>
    public double Cost { get; }

    /// <summary>Initializes a new instance of <see cref="CompassCostAttribute"/>.</summary>
    /// <param name="cost">The estimated cost of invoking proposals from this module.</param>
    public CompassCostAttribute(double cost) { Cost = cost; }
}
