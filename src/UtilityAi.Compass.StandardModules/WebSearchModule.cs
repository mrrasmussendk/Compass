using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Standard module that performs a web search by delegating to the
/// host-provided <see cref="IModelClient"/>. The model is prompted to
/// produce a search-oriented answer for the user's query.
/// </summary>
[CompassCapability("web-search", priority: 3)]
[CompassGoals(GoalTag.Answer)]
[CompassLane(Lane.Execute)]
[CompassCost(0.4)]
[CompassRisk(0.1)]
[CompassCooldown("web-search.query", secondsTtl: 10)]
public sealed class WebSearchModule : ICapabilityModule
{
    private readonly IModelClient _modelClient;

    public WebSearchModule(IModelClient modelClient)
    {
        _modelClient = modelClient;
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        yield return new Proposal(
            id: "web-search.query",
            cons: [new ConstantValue(0.8)],
            act: async ct =>
            {
                var response = await _modelClient.GenerateAsync(
                    new ModelRequest
                    {
                        Prompt = request.Text,
                        SystemMessage = "You are a web search assistant. Provide a concise, factual answer to the user's query as if you had searched the web.",
                        Temperature = 0.3,
                        MaxTokens = 512
                    },
                    ct);
                rt.Bus.Publish(new AiResponse(response.Text));
            }
        ) { Description = "Search the web for an answer" };
    }
}
