using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Workflow;

namespace UtilityAi.Compass.Tests;

public class WorkflowDefinitionTests
{
    [Fact]
    public void WorkflowDefinition_CanBeCreated_WithRequiredProperties()
    {
        var steps = new[]
        {
            new StepDefinition("step-1", "Fetch Data", ["UserRequest"], ["RawData"]),
            new StepDefinition("step-2", "Process Data", ["RawData"], ["ProcessedData"],
                Idempotent: true, MaxRetries: 3, Timeout: TimeSpan.FromSeconds(30))
        };

        var def = new WorkflowDefinition(
            WorkflowId: "wf-data-pipeline",
            DisplayName: "Data Pipeline",
            Goals: [GoalTag.Execute],
            Lanes: [Lane.Execute],
            Steps: steps,
            CanInterrupt: false,
            EstimatedCost: 0.3,
            RiskLevel: 0.1
        );

        Assert.Equal("wf-data-pipeline", def.WorkflowId);
        Assert.Equal("Data Pipeline", def.DisplayName);
        Assert.Single(def.Goals);
        Assert.Equal(2, def.Steps.Length);
        Assert.False(def.CanInterrupt);
    }

    [Fact]
    public void StepDefinition_DefaultsAreCorrect()
    {
        var step = new StepDefinition("step-1", "Test Step", [], []);

        Assert.True(step.Idempotent);
        Assert.Equal(0, step.MaxRetries);
        Assert.Equal(default, step.Timeout);
    }

    [Fact]
    public void StepResult_CanBeCreated_WithOutputFacts()
    {
        var outputs = new Dictionary<string, object>
        {
            ["summary"] = "Completed successfully",
            ["count"] = 42
        };

        var result = new StepResult(StepOutcome.Succeeded, "All good", outputs);

        Assert.Equal(StepOutcome.Succeeded, result.Outcome);
        Assert.Equal("All good", result.Message);
        Assert.Equal(2, result.OutputFacts!.Count);
    }

    [Fact]
    public void StepResult_DefaultsAreNull()
    {
        var result = new StepResult(StepOutcome.Succeeded);

        Assert.Null(result.Message);
        Assert.Null(result.OutputFacts);
    }

    [Fact]
    public void ActiveWorkflow_DefaultsAreCorrect()
    {
        var active = new ActiveWorkflow("wf-1", "run-1", "step-1", WorkflowStatus.Active);

        Assert.False(active.CanInterrupt);
        Assert.Null(active.StickinessUntilUtc);
        Assert.Null(active.BudgetRemaining);
    }

    [Fact]
    public void StepReady_TracksReadiness()
    {
        var ready = new StepReady("wf-1", "step-1", true, []);
        Assert.True(ready.IsReady);
        Assert.Empty(ready.MissingFacts);

        var notReady = new StepReady("wf-1", "step-1", false, ["UserRequest", "ApiKey"]);
        Assert.False(notReady.IsReady);
        Assert.Equal(2, notReady.MissingFacts.Count);
    }

    [Fact]
    public void WorkflowRunRecord_TracksRunLifecycle()
    {
        var started = DateTimeOffset.UtcNow;
        var run = new WorkflowRunRecord("run-1", "wf-1", started);

        Assert.Null(run.EndedUtc);
        Assert.Null(run.Outcome);

        var completed = run with
        {
            EndedUtc = started.AddMinutes(5),
            Outcome = WorkflowStatus.Completed
        };

        Assert.NotNull(completed.EndedUtc);
        Assert.Equal(WorkflowStatus.Completed, completed.Outcome);
    }

    [Fact]
    public void WorkflowStepRecord_TracksStepAttempts()
    {
        var step = new WorkflowStepRecord("run-1", "step-1", 1, DateTimeOffset.UtcNow);
        Assert.Equal(1, step.Attempt);
        Assert.Null(step.Outcome);

        var retry = new WorkflowStepRecord("run-1", "step-1", 2, DateTimeOffset.UtcNow,
            Outcome: StepOutcome.Succeeded, Diagnostics: "Retry succeeded");
        Assert.Equal(2, retry.Attempt);
        Assert.Equal(StepOutcome.Succeeded, retry.Outcome);
    }

    [Fact]
    public void RepairDirective_HoldsRepairTypeAndDetails()
    {
        var directive = new RepairDirective(RepairType.RetryStep, "Step timed out");

        Assert.Equal(RepairType.RetryStep, directive.Repair);
        Assert.Equal("Step timed out", directive.Details);
    }

    [Fact]
    public void NeedsValidation_HoldsScope()
    {
        var need = new NeedsValidation("wf-1", "run-1", ValidationScope.Step, "step-2");

        Assert.Equal(ValidationScope.Step, need.Scope);
        Assert.Equal("step-2", need.TargetId);
    }

    [Fact]
    public void ValidationRecord_StoresOutcome()
    {
        var record = new ValidationRecord("run-1", "step-1", ValidationOutcomeTag.FailRetryable, "Bad format");

        Assert.Equal(ValidationOutcomeTag.FailRetryable, record.Outcome);
        Assert.Equal("Bad format", record.Diagnostics);
    }
}
