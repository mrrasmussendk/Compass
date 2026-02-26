namespace UtilityAi.Nexus.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NexusRiskAttribute : Attribute
{
    public double Risk { get; }
    public NexusRiskAttribute(double risk) { Risk = risk; }
}
