using UtilityAi.Consideration;
using UtilityAi.Memory;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Orchestration;

namespace UtilityAi.Compass.Runtime.Strategy;

/// <summary>
/// Core governance strategy that applies goal/lane filtering, conflict resolution,
/// cooldowns, cost/risk penalties, and hysteresis to select the best <see cref="Proposal"/>.
/// </summary>
public sealed class CompassGovernedSelectionStrategy : ISelectionStrategy
{
    private readonly IMemoryStore _store;
    private readonly IProposalMetadataProvider _metadataProvider;
    private readonly GovernanceConfig _defaultConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompassGovernedSelectionStrategy"/> class.
    /// </summary>
    /// <param name="store">The memory store for governance state lookups.</param>
    /// <param name="metadataProvider">Provider that resolves <see cref="ProposalMetadata"/> for each proposal.</param>
    /// <param name="defaultConfig">Optional default <see cref="GovernanceConfig"/>; falls back to a new instance if <c>null</c>.</param>
    public CompassGovernedSelectionStrategy(
        IMemoryStore store,
        IProposalMetadataProvider metadataProvider,
        GovernanceConfig? defaultConfig = null)
    {
        _store = store;
        _metadataProvider = metadataProvider;
        _defaultConfig = defaultConfig ?? new GovernanceConfig();
    }

    // Proposal ID prefixes that are always allowed through the workflow commitment filter.
    private static readonly string[] SystemPrefixes = ["askuser.", "validate.", "repair."];

    /// <inheritdoc />
    public Proposal Select(IReadOnlyList<(Proposal P, double Utility)> scored, UtilityAi.Utils.Runtime rt)
    {
        if (scored.Count == 0) throw new InvalidOperationException("No proposals to select from.");

        var config = rt.Bus.GetOrDefault<GovernanceConfig>() ?? _defaultConfig;
        var goal = rt.Bus.GetOrDefault<GoalSelected>();
        var lane = rt.Bus.GetOrDefault<LaneSelected>();
        var lastWinner = rt.Bus.GetOrDefault<LastWinner>();
        var activeWorkflow = rt.Bus.GetOrDefault<ActiveWorkflow>();

        var withMeta = scored
            .Select(s => (s.P, s.Utility, Meta: _metadataProvider.GetMetadata(s.P, rt)))
            .ToList();

        withMeta = ApplyWorkflowCommitment(withMeta, activeWorkflow);
        var candidates = FilterByGoalAndLane(withMeta, goal, lane);
        candidates = ResolveConflicts(candidates);
        candidates = ApplyCooldowns(candidates, config, rt);

        if (candidates.Count == 0)
            return scored.OrderBy(s => s.Utility).Last().P;

        var effective = candidates
            .Select(c =>
            {
                var meta = c.Meta;
                var penalty = meta is null ? 0.0
                    : CalculatePenalty(meta, config);
                var effectiveScore = Math.Clamp(c.Utility - penalty, 0.0, 1.0);
                return (c.P, EffectiveScore: effectiveScore, c.Meta);
            })
            .OrderByDescending(c => c.EffectiveScore)
            .ToList();

        if (effective.Count == 0) return scored[0].P;

        var best = effective[0];

        if (lastWinner is not null)
        {
            var lastInList = effective.FirstOrDefault(c => c.P.Id == lastWinner.ProposalId);
            if (lastInList.P is not null)
            {
                var stickyScore = lastInList.EffectiveScore + config.StickinessBonus;
                if (stickyScore >= best.EffectiveScore - config.HysteresisEpsilon)
                    return lastInList.P;
            }
        }

        return best.P;
    }

    private List<(Proposal P, double Utility, ProposalMetadata? Meta)> FilterByGoalAndLane(
        List<(Proposal P, double Utility, ProposalMetadata? Meta)> all,
        GoalSelected? goal, LaneSelected? lane)
    {
        if (goal is not null && lane is not null)
        {
            var both = all.Where(c => c.Meta is not null
                && c.Meta.Goals.Contains(goal.Goal)
                && c.Meta.Lane == lane.Lane).ToList();
            if (both.Count > 0) return both;
        }

        if (goal is not null)
        {
            var goalOnly = all.Where(c => c.Meta is not null && c.Meta.Goals.Contains(goal.Goal)).ToList();
            if (goalOnly.Count > 0) return goalOnly;
        }

        if (lane is not null)
        {
            var laneOnly = all.Where(c => c.Meta is not null && c.Meta.Lane == lane.Lane).ToList();
            if (laneOnly.Count > 0) return laneOnly;
        }

        var untagged = all.Where(c => c.Meta is null).ToList();
        if (untagged.Count > 0) return untagged;

        return all;
    }

    private static List<(Proposal P, double Utility, ProposalMetadata? Meta)> ResolveConflicts(
        List<(Proposal P, double Utility, ProposalMetadata? Meta)> candidates)
    {
        var result = new List<(Proposal P, double Utility, ProposalMetadata? Meta)>();
        var chosenIds = new HashSet<string>();
        var chosenTags = new HashSet<GoalTag>();

        foreach (var candidate in candidates.OrderByDescending(c => c.Utility))
        {
            var meta = candidate.Meta;
            bool conflicted = false;

            if (meta is not null)
            {
                if (meta.ConflictIds?.Any(id => chosenIds.Contains(id)) == true)
                    conflicted = true;

                if (!conflicted && meta.ConflictTags?.Any(tag => chosenTags.Contains(tag)) == true)
                    conflicted = true;
            }

            if (!conflicted)
            {
                result.Add(candidate);
                chosenIds.Add(candidate.P.Id);
                if (meta?.Goals is not null)
                    foreach (var g in meta.Goals) chosenTags.Add(g);
            }
        }

        return result;
    }

    private static List<(Proposal P, double Utility, ProposalMetadata? Meta)> ApplyCooldowns(
        List<(Proposal P, double Utility, ProposalMetadata? Meta)> candidates,
        GovernanceConfig config, UtilityAi.Utils.Runtime rt)
    {
        var result = new List<(Proposal P, double Utility, ProposalMetadata? Meta)>();

        foreach (var candidate in candidates)
        {
            var meta = candidate.Meta;
            if (meta?.CooldownKeyTemplate is null || meta.CooldownTtl is null)
            {
                result.Add(candidate);
                continue;
            }

            var cooldownKey = meta.CooldownKeyTemplate;
            var cooldown = rt.Bus.GetOrDefault<CooldownState>();
            bool onCooldown = cooldown?.Key == cooldownKey && cooldown.IsActive;

            if (onCooldown)
            {
                if (config.HardDropOnCooldown)
                    continue;

                var penalized = Math.Clamp(candidate.Utility * config.CooldownPenalty, 0, 1);
                result.Add((candidate.P, penalized, candidate.Meta));
            }
            else
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    /// <summary>
    /// When an active workflow exists and is not interruptible, drops all proposals
    /// that do not belong to the active workflow, except system proposals
    /// (AskUser, Validate, Repair) which are always allowed through.
    /// </summary>
    private static List<(Proposal P, double Utility, ProposalMetadata? Meta)> ApplyWorkflowCommitment(
        List<(Proposal P, double Utility, ProposalMetadata? Meta)> all,
        ActiveWorkflow? activeWorkflow)
    {
        if (activeWorkflow is null || activeWorkflow.CanInterrupt)
            return all;

        if (activeWorkflow.Status is WorkflowStatus.Completed or WorkflowStatus.Aborted or WorkflowStatus.Idle)
            return all;

        var filtered = all.Where(c =>
            c.P.Id.StartsWith(activeWorkflow.WorkflowId + ".", StringComparison.OrdinalIgnoreCase)
            || IsSystemProposal(c.P.Id))
            .ToList();

        return filtered.Count > 0 ? filtered : all;
    }

    private static bool IsSystemProposal(string proposalId)
    {
        foreach (var prefix in SystemPrefixes)
        {
            if (proposalId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static double CalculatePenalty(ProposalMetadata meta, GovernanceConfig config)
    {
        var minCost = meta.SideEffects switch
        {
            SideEffectLevel.ReadOnly => 0.0,
            SideEffectLevel.Write => 0.2,
            SideEffectLevel.Destructive => 0.4,
            _ => 0.0
        };
        var minRisk = meta.SideEffects switch
        {
            SideEffectLevel.ReadOnly => 0.0,
            SideEffectLevel.Write => 0.35,
            SideEffectLevel.Destructive => 0.7,
            _ => 0.0
        };

        var trustedCost = Math.Clamp(Math.Max(meta.EstimatedCost, minCost), 0.0, 1.0);
        var trustedRisk = Math.Clamp(Math.Max(meta.RiskLevel, minRisk), 0.0, 1.0);
        return config.CostWeight * trustedCost + config.RiskWeight * trustedRisk;
    }
}
