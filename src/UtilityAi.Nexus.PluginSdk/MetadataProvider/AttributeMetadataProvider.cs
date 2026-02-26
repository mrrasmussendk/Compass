using System.Reflection;
using UtilityAi.Consideration;
using UtilityAi.Nexus.Abstractions;
using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Nexus.Abstractions.Interfaces;
using UtilityAi.Nexus.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace UtilityAi.Nexus.PluginSdk.MetadataProvider;

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
        var cap = moduleType.GetCustomAttribute<NexusCapabilityAttribute>();
        var goals = moduleType.GetCustomAttribute<NexusGoalsAttribute>();
        var lane = moduleType.GetCustomAttribute<NexusLaneAttribute>();
        var sideEffects = moduleType.GetCustomAttribute<NexusSideEffectsAttribute>();
        var cost = moduleType.GetCustomAttribute<NexusCostAttribute>();
        var risk = moduleType.GetCustomAttribute<NexusRiskAttribute>();
        var cooldown = moduleType.GetCustomAttribute<NexusCooldownAttribute>();
        var conflicts = moduleType.GetCustomAttribute<NexusConflictsAttribute>();

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
