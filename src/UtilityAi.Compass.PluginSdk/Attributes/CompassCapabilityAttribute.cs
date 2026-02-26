namespace UtilityAi.Compass.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassCapabilityAttribute : Attribute
{
    public string Domain { get; }
    public int Priority { get; }

    public CompassCapabilityAttribute(string domain, int priority = 0)
    {
        Domain = domain;
        Priority = priority;
    }
}
