namespace UtilityAi.Compass.PluginSdk.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassCooldownAttribute : Attribute
{
    public string KeyTemplate { get; }
    public int SecondsTtl { get; }
    public CompassCooldownAttribute(string keyTemplate, int secondsTtl) { KeyTemplate = keyTemplate; SecondsTtl = secondsTtl; }
}
