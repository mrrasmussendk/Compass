using UtilityAi.Compass.Abstractions.Facts;

namespace UtilityAi.Compass.Runtime.DI;

/// <summary>
/// Configuration options for Compass DI registration via <see cref="ServiceCollectionExtensions.AddUtilityAiCompass"/>.
/// </summary>
public sealed class CompassOptions
{
    /// <summary>Gets or sets the <see cref="Facts.GovernanceConfig"/> used by the governance strategy.</summary>
    public GovernanceConfig GovernanceConfig { get; set; } = new();

    /// <summary>Gets or sets whether the <see cref="Modules.GovernanceFinalizerModule"/> is registered.</summary>
    public bool EnableGovernanceFinalizer { get; set; } = true;

    /// <summary>Gets or sets whether Human-in-the-Loop gating is enabled.</summary>
    public bool EnableHitl { get; set; } = false;

    /// <summary>Gets or sets the cooldown keys whose state is tracked each tick.</summary>
    public List<string> TrackedCooldownKeys { get; set; } = [];
}
