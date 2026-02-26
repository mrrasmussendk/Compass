namespace UtilityAi.Compass.PluginSdk.Attributes;

/// <summary>Marks a capability module with its domain name and priority.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class CompassCapabilityAttribute : Attribute
{
    /// <summary>Gets the domain identifier for this capability (e.g. "search", "code-gen").</summary>
    public string Domain { get; }

    /// <summary>Gets the priority used to order proposals from this module. Higher values win ties.</summary>
    public int Priority { get; }

    /// <summary>Initializes a new instance of <see cref="CompassCapabilityAttribute"/>.</summary>
    /// <param name="domain">The domain identifier for this capability.</param>
    /// <param name="priority">Optional priority value; defaults to 0.</param>
    public CompassCapabilityAttribute(string domain, int priority = 0)
    {
        Domain = domain;
        Priority = priority;
    }
}
