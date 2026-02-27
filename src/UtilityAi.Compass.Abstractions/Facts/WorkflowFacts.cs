namespace UtilityAi.Compass.Abstractions.Facts;

/// <summary>
/// Fact representing an active workflow run, published by <c>WorkflowStateSensor</c>.
/// Only one <see cref="ActiveWorkflow"/> should exist per session at a time.
/// </summary>
/// <param name="WorkflowId">Identifier of the workflow definition.</param>
/// <param name="RunId">Unique identifier for this workflow run.</param>
/// <param name="CurrentStepId">The step currently being executed or awaiting execution.</param>
/// <param name="Status">Current lifecycle state of the workflow.</param>
/// <param name="CanInterrupt">Whether the workflow allows interruption by a higher-priority workflow.</param>
/// <param name="StickinessUntilUtc">Optional commitment TTL; the workflow is sticky until this time.</param>
/// <param name="BudgetRemaining">Optional remaining budget for cost-capped workflows.</param>
/// <param name="SessionId">Session identifier scoping this workflow run.</param>
/// <param name="StepAttempt">Current attempt number for the active step (1-based).</param>
/// <param name="MissingFacts">Fact names that are required but not yet available.</param>
/// <param name="ValidationTargetId">Identifier of the step or workflow being validated, if any.</param>
/// <param name="StartedUtc">Timestamp when the workflow run started.</param>
/// <param name="UpdatedUtc">Timestamp of the last state change.</param>
public sealed record ActiveWorkflow(
    string WorkflowId,
    string RunId,
    string? CurrentStepId,
    WorkflowStatus Status,
    bool CanInterrupt = false,
    DateTimeOffset? StickinessUntilUtc = null,
    double? BudgetRemaining = null,
    string? SessionId = null,
    int StepAttempt = 1,
    IReadOnlyList<string>? MissingFacts = null,
    string? ValidationTargetId = null,
    DateTimeOffset? StartedUtc = null,
    DateTimeOffset? UpdatedUtc = null
);

/// <summary>
/// Fact indicating whether a workflow step is ready to execute.
/// Published by <c>WorkflowStateSensor</c> after evaluating step preconditions.
/// </summary>
/// <param name="WorkflowId">Identifier of the workflow definition.</param>
/// <param name="StepId">Identifier of the step being evaluated.</param>
/// <param name="IsReady"><c>true</c> if all preconditions are satisfied.</param>
/// <param name="MissingFacts">Names of facts that are required but not yet available.</param>
/// <param name="SessionId">Session identifier scoping this evaluation.</param>
/// <param name="RunId">Identifier of the parent workflow run.</param>
public sealed record StepReady(
    string WorkflowId,
    string StepId,
    bool IsReady,
    IReadOnlyList<string> MissingFacts,
    string? SessionId = null,
    string? RunId = null
);

/// <summary>
/// Fact requesting validation of a step result or the overall workflow output.
/// Published after step execution or at workflow completion.
/// </summary>
/// <param name="WorkflowId">Identifier of the workflow definition.</param>
/// <param name="RunId">Unique identifier for this workflow run.</param>
/// <param name="Scope">Whether validation targets a single step or the entire workflow.</param>
/// <param name="TargetId">The step or workflow identifier to validate.</param>
/// <param name="SessionId">Session identifier scoping this validation request.</param>
/// <param name="CreatedUtc">Timestamp when the validation was requested.</param>
public sealed record NeedsValidation(
    string WorkflowId,
    string RunId,
    ValidationScope Scope,
    string TargetId,
    string? SessionId = null,
    DateTimeOffset? CreatedUtc = null
);

/// <summary>
/// Fact representing the outcome of a validation check.
/// </summary>
/// <param name="Outcome">Whether the validation passed, failed retryably, or failed fatally.</param>
/// <param name="Diagnostics">Optional human-readable diagnostic message.</param>
/// <param name="SessionId">Session identifier scoping this validation outcome.</param>
/// <param name="WorkflowId">Identifier of the workflow definition.</param>
/// <param name="RunId">Identifier of the parent workflow run.</param>
/// <param name="TargetId">The step or workflow identifier that was validated.</param>
/// <param name="CompletedUtc">Timestamp when the validation completed.</param>
public sealed record ValidationOutcome(
    ValidationOutcomeTag Outcome,
    string? Diagnostics = null,
    string? SessionId = null,
    string? WorkflowId = null,
    string? RunId = null,
    string? TargetId = null,
    DateTimeOffset? CompletedUtc = null
);

/// <summary>
/// Fact directing the system to perform a repair action after a step or validation failure.
/// </summary>
/// <param name="Repair">The type of repair action to take.</param>
/// <param name="Details">Optional details describing the repair action.</param>
/// <param name="SessionId">Session identifier scoping this repair directive.</param>
/// <param name="WorkflowId">Identifier of the workflow definition.</param>
/// <param name="RunId">Identifier of the parent workflow run.</param>
/// <param name="CreatedUtc">Timestamp when the repair was requested.</param>
public sealed record RepairDirective(
    RepairType Repair,
    string? Details = null,
    string? SessionId = null,
    string? WorkflowId = null,
    string? RunId = null,
    DateTimeOffset? CreatedUtc = null
);

/// <summary>
/// Result produced after executing a single workflow step.
/// </summary>
/// <param name="Outcome">The outcome of the step execution.</param>
/// <param name="Message">Optional human-readable message describing the result.</param>
/// <param name="OutputFacts">Key/value pairs of facts produced by the step.</param>
public sealed record StepResult(
    StepOutcome Outcome,
    string? Message = null,
    IReadOnlyDictionary<string, object>? OutputFacts = null
);

/// <summary>
/// Durable record of a workflow run, persisted to the memory store.
/// </summary>
/// <param name="RunId">Unique identifier for this run.</param>
/// <param name="WorkflowId">Identifier of the workflow definition.</param>
/// <param name="StartedUtc">Timestamp when the run began.</param>
/// <param name="EndedUtc">Timestamp when the run ended, or <c>null</c> if still active.</param>
/// <param name="Outcome">Final outcome of the run, or <c>null</c> if still active.</param>
public sealed record WorkflowRunRecord(
    string RunId,
    string WorkflowId,
    DateTimeOffset StartedUtc,
    DateTimeOffset? EndedUtc = null,
    WorkflowStatus? Outcome = null
);

/// <summary>
/// Durable record of a single step execution within a workflow run.
/// </summary>
/// <param name="RunId">Identifier of the parent workflow run.</param>
/// <param name="StepId">Identifier of the step.</param>
/// <param name="Attempt">The attempt number (1-based) for retried steps.</param>
/// <param name="StartedUtc">Timestamp when the step began.</param>
/// <param name="EndedUtc">Timestamp when the step ended, or <c>null</c> if still executing.</param>
/// <param name="Outcome">Outcome of the step execution, or <c>null</c> if still executing.</param>
/// <param name="Diagnostics">Optional diagnostic information for troubleshooting.</param>
public sealed record WorkflowStepRecord(
    string RunId,
    string StepId,
    int Attempt,
    DateTimeOffset StartedUtc,
    DateTimeOffset? EndedUtc = null,
    StepOutcome? Outcome = null,
    string? Diagnostics = null
);

/// <summary>
/// Durable record of a validation check performed during a workflow run.
/// </summary>
/// <param name="RunId">Identifier of the parent workflow run.</param>
/// <param name="TargetId">The step or workflow identifier that was validated.</param>
/// <param name="Outcome">Result of the validation check.</param>
/// <param name="Diagnostics">Optional diagnostic information.</param>
public sealed record ValidationRecord(
    string RunId,
    string TargetId,
    ValidationOutcomeTag Outcome,
    string? Diagnostics = null
);
