using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Nexus.Abstractions;
using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Nexus.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace Nexus.SamplePlugin.Basic;

[NexusCapability("basic", priority: 1)]
[NexusGoals(GoalTag.Answer)]
[NexusLane(Lane.Communicate)]
[NexusCost(0.1)]
public sealed class BasicModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        yield return new Proposal(
            id: "basic.answer",
            cons: [new ConstantValue(0.7)],
            act: _ =>
            {
                rt.Bus.Publish(new AiResponse($"Basic answer to: {request.Text}"));
                return Task.CompletedTask;
            }
        ) { Description = "Basic answer proposal" };

        yield return new Proposal(
            id: "basic.summarize",
            cons: [new ConstantValue(0.5)],
            act: _ =>
            {
                rt.Bus.Publish(new AiResponse($"Summary: {request.Text[..Math.Min(50, request.Text.Length)]}..."));
                return Task.CompletedTask;
            }
        ) { Description = "Basic summarize proposal" };
    }
}
