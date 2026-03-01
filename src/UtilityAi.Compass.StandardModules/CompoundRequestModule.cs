using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Module that detects compound requests and guides the user to submit tasks separately.
/// Uses the MultiStepRequest fact from GoalRouterSensor to detect compound requests.
/// </summary>
[CompassCapability("compound-request-handler", priority: 8)]
[CompassGoals(GoalTag.Execute, GoalTag.Answer)]
[CompassLane(Lane.Interpret)]
[CompassCost(0.1)]
[CompassRisk(0.0)]
public sealed class CompoundRequestModule : ICapabilityModule
{
    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var multiStep = rt.Bus.GetOrDefault<MultiStepRequest>();
        if (multiStep is null || !multiStep.IsCompound)
            yield break;

        yield return new Proposal(
            id: "compound-request.respond",
            cons: [new ConstantValue(0.85)], // High priority to win for compound requests
            act: _ =>
            {
                var response = "I detected multiple independent tasks in your request. " +
                              "For best results, please submit each task as a separate request.\n\n" +
                              "This ensures each task gets proper attention and the results are clear.";

                rt.Bus.Publish(new AiResponse(response));
                return Task.CompletedTask;
            }
        ) { Description = "Respond to compound request with guidance" };
    }
}
