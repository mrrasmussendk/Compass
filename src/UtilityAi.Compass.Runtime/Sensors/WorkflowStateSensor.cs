using UtilityAi.Memory;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Workflow;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.Runtime.Sensors;

/// <summary>
/// Projects active workflow state from <see cref="IMemoryStore"/> onto the EventBus each tick.
/// Publishes <see cref="ActiveWorkflow"/> and <see cref="StepReady"/> facts.
/// </summary>
public sealed class WorkflowStateSensor : ISensor
{
    private readonly IMemoryStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowStateSensor"/> class.
    /// </summary>
    /// <param name="store">The memory store used to recall workflow run state.</param>
    public WorkflowStateSensor(IMemoryStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        var runs = await _store.RecallAsync<WorkflowRunRecord>(
            new MemoryQuery { MaxResults = 1, SortOrder = SortOrder.NewestFirst }, ct);

        var latest = runs.FirstOrDefault();
        if (latest is null) return;

        var run = latest.Fact;

        // Only publish if the run is still active (not completed or aborted)
        if (run.Outcome is WorkflowStatus.Completed or WorkflowStatus.Aborted)
            return;

        var steps = await _store.RecallAsync<WorkflowStepRecord>(
            new MemoryQuery { MaxResults = 100, SortOrder = SortOrder.NewestFirst }, ct);

        var runSteps = steps
            .Where(s => s.Fact.RunId == run.RunId)
            .Select(s => s.Fact)
            .ToList();

        // Determine current step and status
        var latestStep = runSteps.FirstOrDefault();
        string? currentStepId = latestStep?.StepId;

        var status = DeriveStatus(latestStep);

        var active = new ActiveWorkflow(
            WorkflowId: run.WorkflowId,
            RunId: run.RunId,
            CurrentStepId: currentStepId,
            Status: status);

        rt.Bus.Publish(active);
    }

    private static WorkflowStatus DeriveStatus(WorkflowStepRecord? latestStep)
    {
        if (latestStep is null)
            return WorkflowStatus.Active;

        return latestStep.Outcome switch
        {
            null => WorkflowStatus.Active,
            StepOutcome.Succeeded => WorkflowStatus.Active,
            StepOutcome.NeedsUserInput => WorkflowStatus.AwaitingUser,
            StepOutcome.NeedsValidation => WorkflowStatus.Validating,
            StepOutcome.FailedRetryable => WorkflowStatus.Repairing,
            StepOutcome.FailedFatal => WorkflowStatus.Aborted,
            StepOutcome.Cancelled => WorkflowStatus.Aborted,
            _ => WorkflowStatus.Active
        };
    }
}
