namespace UtilityAi.Compass.PluginSdk.Attributes;

/// <summary>Declares cooldown rules that prevent a module's proposals from firing too frequently.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassCooldownAttribute : Attribute
{
    /// <summary>Gets the cooldown key template used to identify the cooldown entry.</summary>
    public string KeyTemplate { get; }

    /// <summary>Gets the cooldown duration in seconds.</summary>
    public int SecondsTtl { get; }

    /// <summary>Initializes a new instance of <see cref="CompassCooldownAttribute"/>.</summary>
    /// <param name="keyTemplate">A key template identifying the cooldown entry (e.g. "my-domain.action").</param>
    /// <param name="secondsTtl">Number of seconds before the proposal may fire again.</param>
    public CompassCooldownAttribute(string keyTemplate, int secondsTtl) { KeyTemplate = keyTemplate; SecondsTtl = secondsTtl; }
}
