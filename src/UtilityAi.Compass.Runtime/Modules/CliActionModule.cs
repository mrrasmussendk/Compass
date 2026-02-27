using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.CliAction;
using UtilityAi.Compass.Abstractions.Facts;

namespace UtilityAi.Compass.Runtime.Modules;

/// <summary>
/// Proposes registered <see cref="ICliAction"/> instances as UtilityAI Proposals
/// when a matching <see cref="CliIntent"/> is detected on the EventBus.
/// </summary>
public sealed class CliActionModule : ICapabilityModule
{
    private readonly IReadOnlyList<ICliAction> _actions;
    private static string VerbToken(CliVerb verb) => verb switch
    {
        CliVerb.Read => "read",
        CliVerb.Write => "write",
        CliVerb.Update => "update",
        _ => verb.ToString().ToLowerInvariant()
    };

    public CliActionModule(IEnumerable<ICliAction> actions)
    {
        _actions = actions.ToList();
    }

    public IEnumerable<Proposal> Propose(UtilityAi.Utils.Runtime rt)
    {
        var intent = rt.Bus.GetOrDefault<CliIntent>();
        if (intent is null) yield break;

        foreach (var action in _actions)
        {
            if (action.Verb != intent.Verb) continue;

            var routeMatch = intent.Target is null
                || action.Route.Equals(intent.Target, StringComparison.OrdinalIgnoreCase);

            var score = routeMatch ? intent.Confidence : intent.Confidence * 0.5;

            var captured = action;
                yield return new Proposal(
                id: $"cli.{VerbToken(action.Verb)}.{action.Route}",
                cons: [new ConstantValue(score)],
                act: async ct =>
                {
                    var instruction = BuildInstruction(intent, captured);
                    var result = await captured.ExecuteAsync(instruction, ct);
                    rt.Bus.Publish(new AiResponse(result));
                }
            ) { Description = action.Description };
        }
    }

    private static string BuildInstruction(CliIntent intent, ICliAction action)
    {
        var verb = VerbToken(intent.Verb);
        return string.IsNullOrWhiteSpace(action.Route) ? verb : $"{verb} {action.Route}";
    }
}
