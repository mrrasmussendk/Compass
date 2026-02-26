using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace Compass.SamplePlugin.Basic;

[CompassCapability("basic", priority: 1)]
[CompassGoals(GoalTag.Answer)]
[CompassLane(Lane.Communicate)]
[CompassCost(0.1)]
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
