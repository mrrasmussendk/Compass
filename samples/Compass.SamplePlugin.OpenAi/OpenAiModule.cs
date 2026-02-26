using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace Compass.SamplePlugin.OpenAi;

[CompassCapability("openai", priority: 2)]
[CompassGoals(GoalTag.Answer)]
[CompassLane(Lane.Communicate)]
[CompassCost(0.5)]
public sealed class OpenAiModule : ICapabilityModule
{
    private readonly IModelClient _modelClient;

    public OpenAiModule(IModelClient modelClient)
    {
        _modelClient = modelClient;
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        yield return new Proposal(
            id: "openai.chat",
            cons: [new ConstantValue(0.9)],
            act: async ct =>
            {
                var response = await _modelClient.GenerateAsync(
                    new ModelRequest { Prompt = request.Text },
                    ct);
                rt.Bus.Publish(new AiResponse(response.Text));
            }
        ) { Description = "Reply using host-provided model" };
    }
}
