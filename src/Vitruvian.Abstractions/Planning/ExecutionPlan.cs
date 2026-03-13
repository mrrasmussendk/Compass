namespace VitruvianAbstractions.Planning;

/// <summary>
/// A single step within a GOAP-style execution plan.
/// Each step maps to a module invocation with explicit dependency edges
/// so that independent steps can run in parallel.
/// </summary>
/// <param name="StepId">Unique identifier for this step within the plan.</param>
/// <param name="ModuleDomain">The domain of the module that should handle this step.</param>
/// <param name="Description">Human-readable description of what this step accomplishes.</param>
/// <param name="Input">The request text to pass to the module.</param>
/// <param name="DependsOn">Step IDs that must complete before this step can execute.</param>
/// <param name="Complexity">
/// Optional complexity hint for this step. When set, model clients may use it to
/// select an appropriate model (e.g. a faster/cheaper model for <see cref="VitruvianAbstractions.Complexity.Low"/>
/// complexity steps). This is entirely optional — framework users are not required to set or act on it.
/// </param>
/// <param name="Precondition">
/// Optional natural-language precondition that must hold before this step executes.
/// When set, every step listed in <see cref="DependsOn"/> must have succeeded; otherwise
/// the step is skipped and marked as failed.
/// </param>
/// <param name="Postcondition">
/// Optional keyword or phrase that must appear (case-insensitive) in the step output
/// for the result to be considered successful. When the check fails the step is marked
/// as failed, even if the module itself did not throw.
/// </param>
/// <param name="FallbackStepId">
/// Optional step ID of a fallback step to execute when this step fails for any reason
/// (precondition, postcondition, module error, or HITL denial). The referenced step
/// must exist in the same plan and is only executed as a fallback — it is skipped during
/// normal wave-based execution.
/// </param>
public sealed record PlanStep(
    string StepId,
    string ModuleDomain,
    string Description,
    string Input,
    IReadOnlyList<string> DependsOn,
    Complexity? Complexity = null,
    string? Precondition = null,
    string? Postcondition = null,
    string? FallbackStepId = null
);

/// <summary>
/// A GOAP-style execution plan: an ordered, dependency-aware graph of steps
/// produced <em>before</em> any execution begins.
/// </summary>
/// <param name="PlanId">Unique identifier for this plan instance.</param>
/// <param name="OriginalRequest">The user request that triggered plan creation.</param>
/// <param name="Steps">The steps to execute, in topological order.</param>
/// <param name="Rationale">LLM-generated explanation of why this plan was chosen.</param>
public sealed record ExecutionPlan(
    string PlanId,
    string OriginalRequest,
    IReadOnlyList<PlanStep> Steps,
    string? Rationale = null
);

/// <summary>
/// The result of executing a single plan step.
/// </summary>
/// <param name="StepId">The step that was executed.</param>
/// <param name="ModuleDomain">The module domain that handled the step.</param>
/// <param name="Success">Whether the step completed successfully.</param>
/// <param name="Output">The output text produced by the module.</param>
/// <param name="ExecutedAt">Timestamp when execution started.</param>
/// <param name="Duration">How long the step took to execute.</param>
/// <param name="WasFallback">Whether this result came from executing a fallback step.</param>
public sealed record PlanStepResult(
    string StepId,
    string ModuleDomain,
    bool Success,
    string Output,
    DateTimeOffset ExecutedAt,
    TimeSpan Duration,
    bool WasFallback = false
);

/// <summary>
/// The aggregated result of executing an entire plan.
/// </summary>
/// <param name="PlanId">The plan that was executed.</param>
/// <param name="Success">Whether all steps completed successfully.</param>
/// <param name="StepResults">Results for each step, in execution order.</param>
/// <param name="AggregatedOutput">Combined output from all steps.</param>
public sealed record PlanResult(
    string PlanId,
    bool Success,
    IReadOnlyList<PlanStepResult> StepResults,
    string AggregatedOutput
);
