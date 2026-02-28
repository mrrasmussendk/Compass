using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Abstractions.Workflow;
using UtilityAi.Compass.Hitl.Facts;

namespace UtilityAi.Compass.Hitl.Modules;

/// <summary>
/// Workflow module that gates destructive operations behind human approval.
/// Two-step workflow: create an approval request, then wait for a decision.
/// </summary>
public sealed class HitlWorkflow : IWorkflowModule
{
    private const double ActionConfidenceThreshold = 0.75;
    private readonly IHumanDecisionChannel _channel;

    /// <summary>Initializes a new instance of <see cref="HitlWorkflow"/>.</summary>
    /// <param name="channel">The channel used to send requests and receive decisions from a human reviewer.</param>
    public HitlWorkflow(IHumanDecisionChannel channel)
    {
        _channel = channel;
    }

    /// <inheritdoc />
    public WorkflowDefinition Define() => new(
        WorkflowId: "hitl",
        DisplayName: "Human-in-the-Loop Gate",
        Goals: [GoalTag.Approve],
        Lanes: [Lane.Safety],
        Steps:
        [
            new StepDefinition(
                StepId: "create-request",
                DisplayName: "Create HITL approval request",
                RequiresFacts: ["UserRequest"],
                ProducesFacts: ["HitlPending", "HitlRequest"],
                Idempotent: false),
            new StepDefinition(
                StepId: "wait-for-decision",
                DisplayName: "Wait for human decision",
                RequiresFacts: ["HitlPending"],
                ProducesFacts: ["HitlApproved", "HitlRejected"],
                Idempotent: true,
                MaxRetries: 0)
        ],
        CanInterrupt: false,
        EstimatedCost: 0.0,
        RiskLevel: 0.0);

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeStart(UtilityAi.Utils.Runtime rt)
    {
        var pending = rt.Bus.GetOrDefault<HitlPending>();
        if (pending is not null) yield break;

        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null || !NeedsHumanApproval(rt, request)) yield break;

        var requestId = Guid.NewGuid().ToString("N");
        yield return new Proposal(
            id: "hitl.create-request",
            cons: [new ConstantValue(0.85)],
            act: async ct =>
            {
                await _channel.SendRequestAsync(requestId, request.Text, ct);
                rt.Bus.Publish(new HitlPending(requestId));
                rt.Bus.Publish(new HitlRequest(requestId, request.Text, "hitl.create-request"));
                rt.Bus.Publish(new StepResult(StepOutcome.Succeeded, "HITL request created"));
            })
        { Description = "Send request to human for approval" };
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeSteps(UtilityAi.Utils.Runtime rt, ActiveWorkflow active)
    {
        if (active.CurrentStepId != "wait-for-decision") yield break;

        var pending = rt.Bus.GetOrDefault<HitlPending>();
        if (pending is null) yield break;

        yield return new Proposal(
            id: "hitl.wait-for-decision",
            cons: [new ConstantValue(0.9)],
            act: async ct =>
            {
                var decision = await _channel.TryReceiveDecisionAsync(pending.RequestId, ct);
                if (decision is true)
                {
                    rt.Bus.Publish(new HitlApproved(pending.RequestId, pending.RequestId));
                    rt.Bus.Publish(new StepResult(StepOutcome.Succeeded, "Approved by human"));
                }
                else if (decision is false)
                {
                    rt.Bus.Publish(new HitlRejected(pending.RequestId, pending.RequestId));
                    rt.Bus.Publish(new StepResult(StepOutcome.Succeeded, "Rejected by human"));
                }
                else
                {
                    rt.Bus.Publish(new StepResult(StepOutcome.NeedsUserInput, "Awaiting human decision"));
                }
            })
        { Description = "Wait for HITL decision" };
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeRepair(UtilityAi.Utils.Runtime rt, ActiveWorkflow active, RepairDirective directive) =>
        [];

    private static bool NeedsHumanApproval(UtilityAi.Utils.Runtime rt, UserRequest request)
    {
        var lane = rt.Bus.GetOrDefault<LaneSelected>();
        if (lane is { Lane: Lane.Execute or Lane.Safety })
            return true;

        var goal = rt.Bus.GetOrDefault<GoalSelected>();
        if (goal is { Goal: GoalTag.Execute or GoalTag.Approve, Confidence: >= ActionConfidenceThreshold })
            return true;

        var intent = rt.Bus.GetOrDefault<CliIntent>();
        if (intent is { Verb: CliVerb.Write or CliVerb.Update, Confidence: >= ActionConfidenceThreshold })
            return true;

        var text = request.Text.ToLowerInvariant();
        var hasDeploy = text.Contains("deploy");
        var hasSocketConnectionPhrase =
            text.Contains("socket connection") ||
            text.Contains("socket connections") ||
            text.Contains("socket-connection") ||
            text.Contains("socket-connections");
        return text.Contains("delete") || text.Contains("override") || (hasDeploy && !hasSocketConnectionPhrase);
    }
}
