using UtilityAi.Compass.Abstractions.Facts;

namespace UtilityAi.Compass.Runtime.DI;

public sealed class CompassOptions
{
    public GovernanceConfig GovernanceConfig { get; set; } = new();
    public bool EnableGovernanceFinalizer { get; set; } = true;
    public bool EnableHitl { get; set; } = false;
    public List<string> TrackedCooldownKeys { get; set; } = [];
}
