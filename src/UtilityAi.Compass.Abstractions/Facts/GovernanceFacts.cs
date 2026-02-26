namespace UtilityAi.Compass.Abstractions.Facts;

public sealed record GovernanceConfig(
    double CostWeight = 0.2,
    double RiskWeight = 0.2,
    double HysteresisEpsilon = 0.05,
    double StickinessBonus = 0.02,
    bool HardDropOnCooldown = false,
    double CooldownPenalty = 0.8
);

public sealed record ProposalMetadata(
    string Domain,
    Lane Lane,
    IReadOnlyList<GoalTag> Goals,
    SideEffectLevel SideEffects = SideEffectLevel.ReadOnly,
    double EstimatedCost = 0.0,
    double RiskLevel = 0.0,
    string? CooldownKeyTemplate = null,
    TimeSpan? CooldownTtl = null,
    IReadOnlyList<string>? ConflictIds = null,
    IReadOnlyList<GoalTag>? ConflictTags = null
);

public sealed record ProposalExecutionRecord(
    string ProposalId,
    string? CorrelationId,
    DateTimeOffset ExecutedAt,
    double Utility,
    OutcomeTag? OutcomeTag = null
);

public sealed record CooldownState(string Key, bool IsActive, TimeSpan? Remaining = null);
public sealed record LastWinner(string ProposalId, DateTimeOffset ChosenAt);
public sealed record BudgetPressure(double Value);
