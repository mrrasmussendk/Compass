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
/// Standard module that summarizes user-provided content using the
/// host-provided <see cref="IModelClient"/>.
/// </summary>
[CompassCapability("summarization", priority: 3)]
[CompassGoals(GoalTag.Summarize)]
[CompassLane(Lane.Communicate)]
[CompassCost(0.3)]
[CompassRisk(0.0)]
public sealed class SummarizationModule : ICapabilityModule
{
    private readonly IModelClient _modelClient;

    public SummarizationModule(IModelClient modelClient)
    {
        _modelClient = modelClient;
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        yield return new Proposal(
            id: "summarization.summarize",
            cons: [new ConstantValue(0.85)],
            act: async ct =>
            {
                var response = await _modelClient.GenerateAsync(
                    new ModelRequest
                    {
                        Prompt = request.Text,
                        SystemMessage = "You are a summarization assistant. Provide a clear and concise summary of the content the user provides.",
                        Temperature = 0.3,
                        MaxTokens = 256
                    },
                    ct);
                rt.Bus.Publish(new AiResponse(response.Text));
            }
        ) { Description = "Summarize the provided content" };
    }
}
