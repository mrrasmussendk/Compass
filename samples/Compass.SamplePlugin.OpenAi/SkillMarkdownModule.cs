using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace Compass.SamplePlugin.OpenAi;

[CompassCapability("openai-skill-md", priority: 2)]
[CompassGoals(GoalTag.Answer)]
[CompassLane(Lane.Communicate)]
[CompassCost(0.5)]
public sealed class SkillMarkdownModule : ICapabilityModule
{
    private const string DefaultSkillPrompt = "You are a helpful assistant. Answer clearly and concisely.";
    private static readonly Lazy<string> SkillPrompt = new(LoadSkillPrompt);
    private readonly IModelClient _modelClient;

    public SkillMarkdownModule(IModelClient modelClient)
    {
        _modelClient = modelClient;
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        yield return new Proposal(
            id: "openai.skill-md.chat",
            cons: [new ConstantValue(0.9)],
            act: async ct =>
            {
                var response = await _modelClient.GenerateAsync(
                    new ModelRequest
                    {
                        Prompt = request.Text,
                        SystemMessage = SkillPrompt.Value,
                        MaxTokens = 512
                    },
                    ct);
                rt.Bus.Publish(new AiResponse(response.Text));
            }
        ) { Description = "Reply using instructions loaded from skill.md" };
    }

    private static string LoadSkillPrompt()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(SkillMarkdownModule).Assembly.Location);
        if (string.IsNullOrWhiteSpace(assemblyDir))
            return DefaultSkillPrompt;

        var skillPath = Path.Combine(assemblyDir, "skill.md");
        if (!File.Exists(skillPath))
            return DefaultSkillPrompt;

        try
        {
            var prompt = File.ReadAllText(skillPath).Trim();
            return string.IsNullOrWhiteSpace(prompt) ? DefaultSkillPrompt : prompt;
        }
        catch (IOException)
        {
            return DefaultSkillPrompt;
        }
        catch (UnauthorizedAccessException)
        {
            return DefaultSkillPrompt;
        }
    }
}
