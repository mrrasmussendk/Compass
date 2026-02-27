using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;

namespace UtilityAi.Compass.Tests;

public class WorkflowAbstractionsTests
{
    [Fact]
    public void ProposalKind_HasAllExpectedValues()
    {
        var values = Enum.GetValues<ProposalKind>();

        Assert.Equal(7, values.Length);
        Assert.Contains(ProposalKind.Atomic, values);
        Assert.Contains(ProposalKind.StartWorkflow, values);
        Assert.Contains(ProposalKind.ContinueStep, values);
        Assert.Contains(ProposalKind.Validate, values);
        Assert.Contains(ProposalKind.Repair, values);
        Assert.Contains(ProposalKind.AskUser, values);
        Assert.Contains(ProposalKind.System, values);
    }

    [Fact]
    public void RepairType_IncludesAbort()
    {
        var values = Enum.GetValues<RepairType>();

        Assert.Contains(RepairType.Abort, values);
    }

    [Fact]
    public void ActiveWorkflow_NewFieldsHaveDefaults()
    {
        var active = new ActiveWorkflow("wf-1", "run-1", "step-1", WorkflowStatus.Active);

        Assert.Null(active.SessionId);
        Assert.Equal(1, active.StepAttempt);
        Assert.Null(active.MissingFacts);
        Assert.Null(active.ValidationTargetId);
        Assert.Null(active.StartedUtc);
        Assert.Null(active.UpdatedUtc);
    }

    [Fact]
    public void ActiveWorkflow_NewFieldsCanBeSet()
    {
        var now = DateTimeOffset.UtcNow;
        var active = new ActiveWorkflow(
            "wf-1", "run-1", "step-1", WorkflowStatus.Validating,
            SessionId: "session-abc",
            StepAttempt: 3,
            MissingFacts: ["UserRequest"],
            ValidationTargetId: "step-1",
            StartedUtc: now,
            UpdatedUtc: now
        );

        Assert.Equal("session-abc", active.SessionId);
        Assert.Equal(3, active.StepAttempt);
        Assert.Single(active.MissingFacts!);
        Assert.Equal("step-1", active.ValidationTargetId);
        Assert.Equal(now, active.StartedUtc);
        Assert.Equal(now, active.UpdatedUtc);
    }

    [Fact]
    public void StepReady_NewFieldsHaveDefaults()
    {
        var ready = new StepReady("wf-1", "step-1", true, []);

        Assert.Null(ready.SessionId);
        Assert.Null(ready.RunId);
    }

    [Fact]
    public void StepReady_NewFieldsCanBeSet()
    {
        var ready = new StepReady("wf-1", "step-1", true, [],
            SessionId: "session-abc", RunId: "run-1");

        Assert.Equal("session-abc", ready.SessionId);
        Assert.Equal("run-1", ready.RunId);
    }

    [Fact]
    public void NeedsValidation_NewFieldsHaveDefaults()
    {
        var need = new NeedsValidation("wf-1", "run-1", ValidationScope.Step, "step-1");

        Assert.Null(need.SessionId);
        Assert.Null(need.CreatedUtc);
    }

    [Fact]
    public void NeedsValidation_NewFieldsCanBeSet()
    {
        var now = DateTimeOffset.UtcNow;
        var need = new NeedsValidation("wf-1", "run-1", ValidationScope.Step, "step-1",
            SessionId: "session-abc", CreatedUtc: now);

        Assert.Equal("session-abc", need.SessionId);
        Assert.Equal(now, need.CreatedUtc);
    }

    [Fact]
    public void ValidationOutcome_NewFieldsHaveDefaults()
    {
        var outcome = new ValidationOutcome(ValidationOutcomeTag.Pass);

        Assert.Null(outcome.SessionId);
        Assert.Null(outcome.WorkflowId);
        Assert.Null(outcome.RunId);
        Assert.Null(outcome.TargetId);
        Assert.Null(outcome.CompletedUtc);
    }

    [Fact]
    public void ValidationOutcome_NewFieldsCanBeSet()
    {
        var now = DateTimeOffset.UtcNow;
        var outcome = new ValidationOutcome(
            ValidationOutcomeTag.FailRetryable,
            Diagnostics: "Schema mismatch",
            SessionId: "session-abc",
            WorkflowId: "wf-1",
            RunId: "run-1",
            TargetId: "step-2",
            CompletedUtc: now
        );

        Assert.Equal("session-abc", outcome.SessionId);
        Assert.Equal("wf-1", outcome.WorkflowId);
        Assert.Equal("run-1", outcome.RunId);
        Assert.Equal("step-2", outcome.TargetId);
        Assert.Equal(now, outcome.CompletedUtc);
    }

    [Fact]
    public void RepairDirective_NewFieldsHaveDefaults()
    {
        var directive = new RepairDirective(RepairType.RetryStep);

        Assert.Null(directive.SessionId);
        Assert.Null(directive.WorkflowId);
        Assert.Null(directive.RunId);
        Assert.Null(directive.CreatedUtc);
    }

    [Fact]
    public void RepairDirective_NewFieldsCanBeSet()
    {
        var now = DateTimeOffset.UtcNow;
        var directive = new RepairDirective(
            RepairType.Abort,
            Details: "Budget exceeded",
            SessionId: "session-abc",
            WorkflowId: "wf-1",
            RunId: "run-1",
            CreatedUtc: now
        );

        Assert.Equal(RepairType.Abort, directive.Repair);
        Assert.Equal("Budget exceeded", directive.Details);
        Assert.Equal("session-abc", directive.SessionId);
        Assert.Equal("wf-1", directive.WorkflowId);
        Assert.Equal("run-1", directive.RunId);
        Assert.Equal(now, directive.CreatedUtc);
    }

    [Fact]
    public void RepairDirective_Abort_CanBeUsed()
    {
        var directive = new RepairDirective(RepairType.Abort, "Fatal error, aborting workflow");

        Assert.Equal(RepairType.Abort, directive.Repair);
        Assert.Equal("Fatal error, aborting workflow", directive.Details);
    }
}
