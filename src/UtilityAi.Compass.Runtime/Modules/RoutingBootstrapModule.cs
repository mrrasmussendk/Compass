using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;

namespace UtilityAi.Compass.Runtime.Modules;

/// <summary>
/// Proposes a clarification prompt when <see cref="GoalSelected"/> confidence is too low,
/// ensuring the user is asked to clarify ambiguous requests.
/// </summary>
public sealed class RoutingBootstrapModule : ICapabilityModule
{
    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(UtilityAi.Utils.Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        var goal = rt.Bus.GetOrDefault<GoalSelected>();
        if (goal is null || goal.Confidence < 0.4)
        {
            yield return new Proposal(
                id: "routing.ask-clarification",
                cons: [new ConstantValue(0.6)],
                act: _ =>
                {
                    rt.Bus.Publish(new AiResponse("Could you please clarify what you'd like me to do?"));
                    rt.Bus.Publish(new GoalSelected(GoalTag.Clarify, 0.95, "bootstrapped"));
                    return Task.CompletedTask;
                }
            ) { Description = "Ask for clarification when goal is ambiguous" };
        }
    }
}
