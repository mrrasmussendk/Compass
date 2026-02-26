namespace UtilityAi.Compass.Abstractions.Facts;

/// <summary>
/// Governance tuning knobs that control how proposals are scored and filtered.
/// Published as a fact so sensors and the selection strategy can read the current configuration.
/// </summary>
/// <param name="CostWeight">Multiplier applied to estimated cost when penalising a proposal's score.</param>
/// <param name="RiskWeight">Multiplier applied to risk level when penalising a proposal's score.</param>
/// <param name="HysteresisEpsilon">Minimum score delta required before the strategy switches away from the previous winner.</param>
/// <param name="StickinessBonus">Bonus added to the previous winner's score to reduce oscillation.</param>
/// <param name="HardDropOnCooldown">When <c>true</c>, proposals on cooldown are removed entirely instead of being penalised.</param>
/// <param name="CooldownPenalty">Score penalty applied to proposals that are on cooldown (used when <paramref name="HardDropOnCooldown"/> is <c>false</c>).</param>
public sealed record GovernanceConfig(
    double CostWeight = 0.2,
    double RiskWeight = 0.2,
    double HysteresisEpsilon = 0.05,
    double StickinessBonus = 0.02,
    bool HardDropOnCooldown = false,
    double CooldownPenalty = 0.8
);

/// <summary>
/// Rich metadata attached to a proposal by the plugin SDK via attributes.
/// Used by the governance strategy for routing, conflict detection, and cost/risk scoring.
/// </summary>
/// <param name="Domain">Logical domain identifier for the capability (e.g. "my-domain").</param>
/// <param name="Lane">The <see cref="Abstractions.Lane"/> this proposal belongs to.</param>
/// <param name="Goals">Goal tags that this proposal can satisfy.</param>
/// <param name="SideEffects">The level of side effects this proposal may produce.</param>
/// <param name="EstimatedCost">Estimated token or monetary cost in the range [0, 1].</param>
/// <param name="RiskLevel">Risk level in the range [0, 1].</param>
/// <param name="CooldownKeyTemplate">Optional key template used to track cooldown state.</param>
/// <param name="CooldownTtl">Optional time-to-live for the cooldown period.</param>
/// <param name="ConflictIds">Optional list of proposal IDs that conflict with this proposal.</param>
/// <param name="ConflictTags">Optional list of <see cref="GoalTag"/> values that conflict with this proposal.</param>
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

/// <summary>Execution log entry recorded after a proposal has been executed.</summary>
/// <param name="ProposalId">Identifier of the executed proposal.</param>
/// <param name="CorrelationId">Optional correlation identifier linking to the originating tick.</param>
/// <param name="ExecutedAt">Timestamp when the proposal was executed.</param>
/// <param name="Utility">The final utility score at the time of selection.</param>
/// <param name="OutcomeTag">Optional outcome tag indicating the result of execution.</param>
public sealed record ProposalExecutionRecord(
    string ProposalId,
    string? CorrelationId,
    DateTimeOffset ExecutedAt,
    double Utility,
    OutcomeTag? OutcomeTag = null
);

/// <summary>Fact indicating the cooldown status of a specific proposal key.</summary>
/// <param name="Key">The cooldown key identifying the proposal or action.</param>
/// <param name="IsActive">Whether the cooldown is currently active.</param>
/// <param name="Remaining">Optional time remaining until the cooldown expires.</param>
public sealed record CooldownState(string Key, bool IsActive, TimeSpan? Remaining = null);

/// <summary>Fact recording the most recently selected proposal for hysteresis tracking.</summary>
/// <param name="ProposalId">Identifier of the winning proposal.</param>
/// <param name="ChosenAt">Timestamp when the proposal was selected.</param>
public sealed record LastWinner(string ProposalId, DateTimeOffset ChosenAt);

/// <summary>Fact representing token or monetary budget pressure, used to bias towards lower-cost proposals.</summary>
/// <param name="Value">Budget pressure value in the range [0, 1] where 1 indicates maximum pressure.</param>
public sealed record BudgetPressure(double Value);
