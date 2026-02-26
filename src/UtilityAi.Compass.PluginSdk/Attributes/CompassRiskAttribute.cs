namespace UtilityAi.Compass.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassRiskAttribute : Attribute
{
    public double Risk { get; }
    public CompassRiskAttribute(double risk) { Risk = risk; }
}
