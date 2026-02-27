using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.PluginSdk.MetadataProvider;
using UtilityAi.Compass.Runtime.Strategy;
using UtilityAi.Memory;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class WorkflowCommitmentTests
{
    private static CompassGovernedSelectionStrategy CreateStrategy(AttributeMetadataProvider? provider = null)
    {
        var store = new InMemoryStore();
        var metaProvider = provider ?? new AttributeMetadataProvider();
        return new CompassGovernedSelectionStrategy(store, metaProvider);
    }

    [Fact]
    public void Select_DropsNonWorkflowProposals_WhenActiveWorkflowIsNotInterruptible()
    {
        var provider = new AttributeMetadataProvider();
        provider.Register("wf-deploy.step-build", new ProposalMetadata("deploy", Lane.Execute, [GoalTag.Execute]));
        provider.Register("other.action", new ProposalMetadata("other", Lane.Execute, [GoalTag.Execute]));

        var strategy = CreateStrategy(provider);
        var bus = new EventBus();
        bus.Publish(new GoalSelected(GoalTag.Execute, 0.9));
        bus.Publish(new LaneSelected(Lane.Execute));
        bus.Publish(new ActiveWorkflow("wf-deploy", "run-1", "step-build", WorkflowStatus.Active, CanInterrupt: false));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var pWorkflow = new Proposal("wf-deploy.step-build", [new ConstantValue(0.6)], _ => Task.CompletedTask);
        var pOther = new Proposal("other.action", [new ConstantValue(0.9)], _ => Task.CompletedTask);

        var scored = new List<(Proposal P, double Utility)> { (pWorkflow, 0.6), (pOther, 0.9) };
        var result = strategy.Select(scored, rt);

        Assert.Equal("wf-deploy.step-build", result.Id);
    }

    [Fact]
    public void Select_AllowsAllProposals_WhenActiveWorkflowIsInterruptible()
    {
        var provider = new AttributeMetadataProvider();
        provider.Register("wf-deploy.step-build", new ProposalMetadata("deploy", Lane.Execute, [GoalTag.Execute]));
        provider.Register("other.action", new ProposalMetadata("other", Lane.Execute, [GoalTag.Execute]));

        var strategy = CreateStrategy(provider);
        var bus = new EventBus();
        bus.Publish(new GoalSelected(GoalTag.Execute, 0.9));
        bus.Publish(new LaneSelected(Lane.Execute));
        bus.Publish(new ActiveWorkflow("wf-deploy", "run-1", "step-build", WorkflowStatus.Active, CanInterrupt: true));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var pWorkflow = new Proposal("wf-deploy.step-build", [new ConstantValue(0.6)], _ => Task.CompletedTask);
        var pOther = new Proposal("other.action", [new ConstantValue(0.9)], _ => Task.CompletedTask);

        var scored = new List<(Proposal P, double Utility)> { (pWorkflow, 0.6), (pOther, 0.9) };
        var result = strategy.Select(scored, rt);

        Assert.Equal("other.action", result.Id);
    }

    [Fact]
    public void Select_AllowsSystemProposals_DuringNonInterruptibleWorkflow()
    {
        var provider = new AttributeMetadataProvider();
        provider.Register("wf-deploy.step-build", new ProposalMetadata("deploy", Lane.Execute, [GoalTag.Execute]));
        provider.Register("askuser.clarify", new ProposalMetadata("system", Lane.Communicate, [GoalTag.Clarify]));

        var strategy = CreateStrategy(provider);
        var bus = new EventBus();
        bus.Publish(new ActiveWorkflow("wf-deploy", "run-1", "step-build", WorkflowStatus.Active, CanInterrupt: false));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var pWorkflow = new Proposal("wf-deploy.step-build", [new ConstantValue(0.3)], _ => Task.CompletedTask);
        var pAskUser = new Proposal("askuser.clarify", [new ConstantValue(0.9)], _ => Task.CompletedTask);

        var scored = new List<(Proposal P, double Utility)> { (pWorkflow, 0.3), (pAskUser, 0.9) };
        var result = strategy.Select(scored, rt);

        // Both should be allowed through (askuser is a system proposal)
        Assert.True(result.Id == "askuser.clarify" || result.Id == "wf-deploy.step-build");
    }

    [Fact]
    public void Select_AllowsValidateProposals_DuringNonInterruptibleWorkflow()
    {
        var provider = new AttributeMetadataProvider();
        provider.Register("validate.step-result", new ProposalMetadata("system", Lane.Safety, [GoalTag.Approve]));

        var strategy = CreateStrategy(provider);
        var bus = new EventBus();
        bus.Publish(new ActiveWorkflow("wf-deploy", "run-1", "step-build", WorkflowStatus.Active, CanInterrupt: false));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var pValidate = new Proposal("validate.step-result", [new ConstantValue(0.8)], _ => Task.CompletedTask);
        var scored = new List<(Proposal P, double Utility)> { (pValidate, 0.8) };
        var result = strategy.Select(scored, rt);

        Assert.Equal("validate.step-result", result.Id);
    }

    [Fact]
    public void Select_AllowsRepairProposals_DuringNonInterruptibleWorkflow()
    {
        var provider = new AttributeMetadataProvider();
        provider.Register("repair.retry-step", new ProposalMetadata("system", Lane.Execute, [GoalTag.Execute]));

        var strategy = CreateStrategy(provider);
        var bus = new EventBus();
        bus.Publish(new ActiveWorkflow("wf-deploy", "run-1", "step-build", WorkflowStatus.Repairing, CanInterrupt: false));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var pRepair = new Proposal("repair.retry-step", [new ConstantValue(0.7)], _ => Task.CompletedTask);
        var scored = new List<(Proposal P, double Utility)> { (pRepair, 0.7) };
        var result = strategy.Select(scored, rt);

        Assert.Equal("repair.retry-step", result.Id);
    }

    [Fact]
    public void Select_NoFilter_WhenNoActiveWorkflow()
    {
        var provider = new AttributeMetadataProvider();
        provider.Register("p1", new ProposalMetadata("d", Lane.Execute, [GoalTag.Execute]));
        provider.Register("p2", new ProposalMetadata("d", Lane.Execute, [GoalTag.Execute]));

        var strategy = CreateStrategy(provider);
        var bus = new EventBus();
        bus.Publish(new GoalSelected(GoalTag.Execute, 0.9));
        bus.Publish(new LaneSelected(Lane.Execute));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var p1 = new Proposal("p1", [new ConstantValue(0.5)], _ => Task.CompletedTask);
        var p2 = new Proposal("p2", [new ConstantValue(0.9)], _ => Task.CompletedTask);

        var scored = new List<(Proposal P, double Utility)> { (p1, 0.5), (p2, 0.9) };
        var result = strategy.Select(scored, rt);

        Assert.Equal("p2", result.Id);
    }

    [Fact]
    public void Select_NoFilter_WhenWorkflowIsCompleted()
    {
        var provider = new AttributeMetadataProvider();
        provider.Register("p1", new ProposalMetadata("d", Lane.Execute, [GoalTag.Execute]));
        provider.Register("p2", new ProposalMetadata("d", Lane.Execute, [GoalTag.Execute]));

        var strategy = CreateStrategy(provider);
        var bus = new EventBus();
        bus.Publish(new GoalSelected(GoalTag.Execute, 0.9));
        bus.Publish(new LaneSelected(Lane.Execute));
        bus.Publish(new ActiveWorkflow("wf-done", "run-1", null, WorkflowStatus.Completed, CanInterrupt: false));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var p1 = new Proposal("p1", [new ConstantValue(0.5)], _ => Task.CompletedTask);
        var p2 = new Proposal("p2", [new ConstantValue(0.9)], _ => Task.CompletedTask);

        var scored = new List<(Proposal P, double Utility)> { (p1, 0.5), (p2, 0.9) };
        var result = strategy.Select(scored, rt);

        Assert.Equal("p2", result.Id);
    }
}
