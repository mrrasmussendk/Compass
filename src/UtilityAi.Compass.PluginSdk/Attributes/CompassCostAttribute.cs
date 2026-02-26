namespace UtilityAi.Compass.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassCostAttribute : Attribute
{
    public double Cost { get; }
    public CompassCostAttribute(double cost) { Cost = cost; }
}
