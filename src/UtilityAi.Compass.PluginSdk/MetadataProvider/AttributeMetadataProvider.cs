using System.Reflection;
using UtilityAi.Consideration;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace UtilityAi.Compass.PluginSdk.MetadataProvider;

/// <summary>
/// Reads Compass SDK attributes from module types and provides
/// <see cref="ProposalMetadata"/> to the governance strategy.
/// </summary>
public sealed class AttributeMetadataProvider : IProposalMetadataProvider
{
    private readonly Dictionary<string, ProposalMetadata> _registry = new();
    private readonly Dictionary<string, Type> _proposalModuleTypes = new();

    /// <summary>Registers explicit <see cref="ProposalMetadata"/> for a given proposal identifier.</summary>
    /// <param name="proposalId">The unique proposal identifier.</param>
    /// <param name="metadata">The metadata to associate with the proposal.</param>
    public void Register(string proposalId, ProposalMetadata metadata)
    {
        _registry[proposalId] = metadata;
    }

    /// <summary>Associates a module <see cref="Type"/> with a proposal so metadata can be built from its attributes.</summary>
    /// <param name="proposalId">The unique proposal identifier.</param>
    /// <param name="moduleType">The module type whose attributes will be inspected.</param>
    public void RegisterModuleType(string proposalId, Type moduleType)
    {
        _proposalModuleTypes[proposalId] = moduleType;
    }

    /// <summary>Returns the <see cref="ProposalMetadata"/> for a proposal, building it from attributes if needed.</summary>
    /// <param name="proposal">The proposal to look up metadata for.</param>
    /// <param name="rt">The current utility-AI runtime.</param>
    /// <returns>The metadata, or <c>null</c> if none could be resolved.</returns>
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
