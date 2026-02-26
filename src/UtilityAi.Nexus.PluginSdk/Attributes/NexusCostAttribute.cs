namespace UtilityAi.Nexus.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NexusCostAttribute : Attribute
{
    public double Cost { get; }
    public NexusCostAttribute(double cost) { Cost = cost; }
}
