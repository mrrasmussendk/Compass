using UtilityAi.Consideration;
using UtilityAi.Memory;
using UtilityAi.Nexus.Abstractions;
using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Nexus.Abstractions.Interfaces;
using UtilityAi.Orchestration;

namespace UtilityAi.Nexus.Runtime.Strategy;

public sealed class NexusGovernedSelectionStrategy : ISelectionStrategy
{
    private readonly IMemoryStore _store;
    private readonly IProposalMetadataProvider _metadataProvider;
    private readonly GovernanceConfig _defaultConfig;

    public NexusGovernedSelectionStrategy(
        IMemoryStore store,
        IProposalMetadataProvider metadataProvider,
        GovernanceConfig? defaultConfig = null)
    {
        _store = store;
        _metadataProvider = metadataProvider;
        _defaultConfig = defaultConfig ?? new GovernanceConfig();
    }

    public Proposal Select(IReadOnlyList<(Proposal P, double Utility)> scored, UtilityAi.Utils.Runtime rt)
    {
        if (scored.Count == 0) throw new InvalidOperationException("No proposals to select from.");

        var config = rt.Bus.GetOrDefault<GovernanceConfig>() ?? _defaultConfig;
        var goal = rt.Bus.GetOrDefault<GoalSelected>();
        var lane = rt.Bus.GetOrDefault<LaneSelected>();
        var lastWinner = rt.Bus.GetOrDefault<LastWinner>();

        var withMeta = scored
            .Select(s => (s.P, s.Utility, Meta: _metadataProvider.GetMetadata(s.P, rt)))
            .ToList();

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
                    : config.CostWeight * meta.EstimatedCost + config.RiskWeight * meta.RiskLevel;
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
}
