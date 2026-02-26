namespace UtilityAi.Nexus.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class NexusCooldownAttribute : Attribute
{
    public string KeyTemplate { get; }
    public int SecondsTtl { get; }
    public NexusCooldownAttribute(string keyTemplate, int secondsTtl) { KeyTemplate = keyTemplate; SecondsTtl = secondsTtl; }
}
