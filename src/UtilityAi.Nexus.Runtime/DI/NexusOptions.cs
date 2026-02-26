using UtilityAi.Nexus.Abstractions.Facts;

namespace UtilityAi.Nexus.Runtime.DI;

public sealed class NexusOptions
{
    public GovernanceConfig GovernanceConfig { get; set; } = new();
    public bool EnableGovernanceFinalizer { get; set; } = true;
    public bool EnableHitl { get; set; } = false;
    public List<string> TrackedCooldownKeys { get; set; } = [];
}
