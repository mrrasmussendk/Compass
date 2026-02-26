using System.Reflection;
using UtilityAi.Consideration;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace UtilityAi.Compass.PluginSdk.MetadataProvider;

public sealed class AttributeMetadataProvider : IProposalMetadataProvider
{
    private readonly Dictionary<string, ProposalMetadata> _registry = new();
    private readonly Dictionary<string, Type> _proposalModuleTypes = new();

    public void Register(string proposalId, ProposalMetadata metadata)
    {
        _registry[proposalId] = metadata;
    }

    public void RegisterModuleType(string proposalId, Type moduleType)
    {
        _proposalModuleTypes[proposalId] = moduleType;
    }

    public ProposalMetadata? GetMetadata(Proposal proposal, Runtime rt)
    {
        if (_registry.TryGetValue(proposal.Id, out var meta))
            return meta;

        if (!_proposalModuleTypes.TryGetValue(proposal.Id, out var moduleType))
            return null;

        return BuildFromAttributes(moduleType);
    }

    private static ProposalMetadata? BuildFromAttributes(Type moduleType)
    {
        var cap = moduleType.GetCustomAttribute<CompassCapabilityAttribute>();
        var goals = moduleType.GetCustomAttribute<CompassGoalsAttribute>();
        var lane = moduleType.GetCustomAttribute<CompassLaneAttribute>();
        var sideEffects = moduleType.GetCustomAttribute<CompassSideEffectsAttribute>();
        var cost = moduleType.GetCustomAttribute<CompassCostAttribute>();
        var risk = moduleType.GetCustomAttribute<CompassRiskAttribute>();
        var cooldown = moduleType.GetCustomAttribute<CompassCooldownAttribute>();
        var conflicts = moduleType.GetCustomAttribute<CompassConflictsAttribute>();

        if (cap is null && goals is null && lane is null) return null;

        return new ProposalMetadata(
            Domain: cap?.Domain ?? "unknown",
            Lane: lane?.Lane ?? Lane.Interpret,
            Goals: (IReadOnlyList<GoalTag>)(goals?.Goals ?? Array.Empty<GoalTag>()),
            SideEffects: sideEffects?.Level ?? SideEffectLevel.ReadOnly,
            EstimatedCost: cost?.Cost ?? 0.0,
            RiskLevel: risk?.Risk ?? 0.0,
            CooldownKeyTemplate: cooldown?.KeyTemplate,
            CooldownTtl: cooldown is not null ? TimeSpan.FromSeconds(cooldown.SecondsTtl) : null,
            ConflictIds: conflicts?.Ids,
            ConflictTags: conflicts?.Tags
        );
    }
}
