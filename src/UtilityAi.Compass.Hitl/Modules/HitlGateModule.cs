using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Hitl.Facts;

namespace UtilityAi.Compass.Hitl.Modules;

/// <summary>
/// Capability module that gates destructive operations behind human approval
/// using an <see cref="IHumanDecisionChannel"/>.
/// </summary>
public sealed class HitlGateModule : ICapabilityModule
{
    private readonly IHumanDecisionChannel _channel;

    /// <summary>Initializes a new instance of <see cref="HitlGateModule"/>.</summary>
    /// <param name="channel">The channel used to send requests and receive decisions from a human reviewer.</param>
    public HitlGateModule(IHumanDecisionChannel channel)
    {
        _channel = channel;
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(UtilityAi.Utils.Runtime rt)
    {
        var pending = rt.Bus.GetOrDefault<HitlPending>();

        if (pending is not null)
        {
            yield return new Proposal(
                id: "hitl.wait-for-decision",
                cons: [new ConstantValue(0.9)],
                act: async ct =>
                {
                    var decision = await _channel.TryReceiveDecisionAsync(pending.RequestId, ct);
                    if (decision is true)
                        rt.Bus.Publish(new HitlApproved(pending.RequestId, pending.RequestId));
                    else if (decision is false)
                        rt.Bus.Publish(new HitlRejected(pending.RequestId, pending.RequestId));
                }
            ) { Description = "Wait for HITL decision" };
            yield break;
        }

        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is not null && NeedsHumanApproval(request))
        {
            var requestId = Guid.NewGuid().ToString("N");
            yield return new Proposal(
                id: "hitl.create-request",
                cons: [new ConstantValue(0.85)],
                act: async ct =>
                {
                    await _channel.SendRequestAsync(requestId, request.Text, ct);
                    rt.Bus.Publish(new HitlPending(requestId));
                    rt.Bus.Publish(new HitlRequest(requestId, request.Text, "hitl.create-request"));
                }
            ) { Description = "Send request to human for approval" };
        }
    }

    private static bool NeedsHumanApproval(UserRequest request)
    {
        var text = request.Text.ToLowerInvariant();
        var isSocketDeployRequest = text.Contains("deploy") && text.Contains("socket connection");
        return text.Contains("delete") || (text.Contains("deploy") && !isSocketDeployRequest) || text.Contains("override");
    }
}
