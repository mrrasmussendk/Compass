namespace UtilityAi.Nexus.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NexusCapabilityAttribute : Attribute
{
    public string Domain { get; }
    public int Priority { get; }

    public NexusCapabilityAttribute(string domain, int priority = 0)
    {
        Domain = domain;
        Priority = priority;
    }
}
