namespace UtilityAi.Compass.Abstractions.Workflow;

/// <summary>
/// Declares a multi-step workflow that a plugin provides.
/// Describes the steps, goal/lane alignment, and governance metadata.
/// </summary>
/// <param name="WorkflowId">Unique identifier for this workflow definition.</param>
/// <param name="DisplayName">Human-readable name for the workflow.</param>
/// <param name="Goals">Goal tags that this workflow can satisfy.</param>
/// <param name="Lanes">Lanes this workflow operates in.</param>
/// <param name="Steps">Ordered list of step definitions comprising the workflow.</param>
/// <param name="CanInterrupt">Whether this workflow allows interruption by higher-priority workflows.</param>
/// <param name="EstimatedCost">Estimated total cost in the range [0, 1].</param>
/// <param name="RiskLevel">Risk level in the range [0, 1].</param>
public sealed record WorkflowDefinition(
    string WorkflowId,
    string DisplayName,
    GoalTag[] Goals,
    Lane[] Lanes,
    StepDefinition[] Steps,
    bool CanInterrupt = false,
    double EstimatedCost = 0.0,
    double RiskLevel = 0.0
);

/// <summary>
/// Declares a single step within a <see cref="WorkflowDefinition"/>.
/// </summary>
/// <param name="StepId">Unique identifier for this step within its workflow.</param>
/// <param name="DisplayName">Human-readable name for the step.</param>
/// <param name="RequiresFacts">Fact type names or keys that must be present before the step can execute.</param>
/// <param name="ProducesFacts">Fact type names or keys that this step produces on success.</param>
/// <param name="Idempotent">Whether the step is safe to retry without side-effects.</param>
/// <param name="MaxRetries">Maximum number of retry attempts for retryable failures.</param>
/// <param name="Timeout">Maximum duration the step is allowed to execute.</param>
public sealed record StepDefinition(
    string StepId,
    string DisplayName,
    string[] RequiresFacts,
    string[] ProducesFacts,
    bool Idempotent = true,
    int MaxRetries = 0,
    TimeSpan Timeout = default
);
