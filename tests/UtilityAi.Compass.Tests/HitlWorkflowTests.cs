using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Hitl.Facts;
using UtilityAi.Compass.Hitl.Modules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class HitlWorkflowTests
{
    private sealed class NoopHumanDecisionChannel : IHumanDecisionChannel
    {
        public Task SendRequestAsync(string requestId, string description, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool?> TryReceiveDecisionAsync(string requestId, CancellationToken ct = default)
            => Task.FromResult<bool?>(null);
    }

    private sealed class ApprovingChannel : IHumanDecisionChannel
    {
        public Task SendRequestAsync(string requestId, string description, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool?> TryReceiveDecisionAsync(string requestId, CancellationToken ct = default)
            => Task.FromResult<bool?>(true);
    }

    [Fact]
    public void Define_ReturnsCorrectWorkflowDefinition()
    {
        var wf = new HitlWorkflow(new NoopHumanDecisionChannel());
        var def = wf.Define();

        Assert.Equal("hitl", def.WorkflowId);
        Assert.Equal("Human-in-the-Loop Gate", def.DisplayName);
        Assert.Contains(GoalTag.Approve, def.Goals);
        Assert.Contains(Lane.Safety, def.Lanes);
        Assert.Equal(2, def.Steps.Length);
        Assert.Equal("create-request", def.Steps[0].StepId);
        Assert.Equal("wait-for-decision", def.Steps[1].StepId);
        Assert.False(def.CanInterrupt);
    }

    [Fact]
    public void ProposeStart_ReturnsProposal_ForDeployRequest()
    {
        var wf = new HitlWorkflow(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        bus.Publish(new UserRequest("deploy this service"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = wf.ProposeStart(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("hitl.create-request", proposals[0].Id);
    }

    [Fact]
    public void ProposeStart_ReturnsEmpty_ForNonDestructiveRequest()
    {
        var wf = new HitlWorkflow(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        bus.Publish(new UserRequest("read file notes.txt"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        Assert.Empty(wf.ProposeStart(rt));
    }

    [Fact]
    public void ProposeStart_ReturnsEmpty_WhenPendingExists()
    {
        var wf = new HitlWorkflow(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        bus.Publish(new UserRequest("deploy this service"));
        bus.Publish(new HitlPending("existing-request"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        Assert.Empty(wf.ProposeStart(rt));
    }

    [Theory]
    [InlineData("allow deploy for a socket connection")]
    [InlineData("allow deploy for a socket-connection")]
    [InlineData("allow deploy for socket connections")]
    public void ProposeStart_ReturnsEmpty_ForSocketDeployRequest(string requestText)
    {
        var wf = new HitlWorkflow(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        bus.Publish(new UserRequest(requestText));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        Assert.Empty(wf.ProposeStart(rt));
    }

    [Fact]
    public void ProposeStart_ReturnsProposal_ForExecuteLaneEvenWithoutRiskKeyword()
    {
        var wf = new HitlWorkflow(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        bus.Publish(new UserRequest("please proceed"));
        bus.Publish(new LaneSelected(Lane.Execute));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = wf.ProposeStart(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("hitl.create-request", proposals[0].Id);
    }

    [Fact]
    public async Task ProposeStart_ActPublishesHitlFacts_WhenExecuted()
    {
        var wf = new HitlWorkflow(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        bus.Publish(new UserRequest("deploy this service"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = wf.ProposeStart(rt).ToList();
        await proposals[0].Act(CancellationToken.None);

        Assert.NotNull(bus.GetOrDefault<HitlPending>());
        Assert.NotNull(bus.GetOrDefault<HitlRequest>());
        Assert.NotNull(bus.GetOrDefault<StepResult>());
        Assert.Equal(StepOutcome.Succeeded, bus.GetOrDefault<StepResult>()!.Outcome);
    }

    [Fact]
    public void ProposeSteps_ReturnsWaitProposal_WhenOnWaitStep()
    {
        var wf = new HitlWorkflow(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        bus.Publish(new HitlPending("req-1"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);
        var active = new ActiveWorkflow("hitl", "run-1", "wait-for-decision", WorkflowStatus.Active);

        var proposals = wf.ProposeSteps(rt, active).ToList();

        Assert.Single(proposals);
        Assert.Equal("hitl.wait-for-decision", proposals[0].Id);
    }

    [Fact]
    public void ProposeSteps_ReturnsEmpty_WhenNotOnWaitStep()
    {
        var wf = new HitlWorkflow(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);
        var active = new ActiveWorkflow("hitl", "run-1", "create-request", WorkflowStatus.Active);

        Assert.Empty(wf.ProposeSteps(rt, active));
    }

    [Fact]
    public async Task ProposeSteps_WaitPublishesApproved_WhenDecisionIsTrue()
    {
        var wf = new HitlWorkflow(new ApprovingChannel());
        var bus = new EventBus();
        bus.Publish(new HitlPending("req-1"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);
        var active = new ActiveWorkflow("hitl", "run-1", "wait-for-decision", WorkflowStatus.Active);

        var proposals = wf.ProposeSteps(rt, active).ToList();
        await proposals[0].Act(CancellationToken.None);

        Assert.NotNull(bus.GetOrDefault<HitlApproved>());
        Assert.Equal(StepOutcome.Succeeded, bus.GetOrDefault<StepResult>()!.Outcome);
    }

    [Fact]
    public async Task ProposeSteps_WaitPublishesNeedsUserInput_WhenNoDecision()
    {
        var wf = new HitlWorkflow(new NoopHumanDecisionChannel());
        var bus = new EventBus();
        bus.Publish(new HitlPending("req-1"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);
        var active = new ActiveWorkflow("hitl", "run-1", "wait-for-decision", WorkflowStatus.Active);

        var proposals = wf.ProposeSteps(rt, active).ToList();
        await proposals[0].Act(CancellationToken.None);

        Assert.Equal(StepOutcome.NeedsUserInput, bus.GetOrDefault<StepResult>()!.Outcome);
    }
}
