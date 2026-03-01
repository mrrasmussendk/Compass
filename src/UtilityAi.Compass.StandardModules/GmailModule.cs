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
/// Standard module for Gmail-related tasks with read and draft-write support.
/// </summary>
[CompassCapability("gmail", priority: 3)]
[CompassGoals(GoalTag.Answer, GoalTag.Execute)]
[CompassLane(Lane.Execute)]
[CompassCost(0.5)]
[CompassRisk(0.3)]
[CompassSideEffects(SideEffectLevel.Write)]
public sealed class GmailModule : ICapabilityModule
{
    private readonly IModelClient? _modelClient;

    public static readonly ModelTool GmailReadTool = new(
        "gmail_read_messages",
        "Read messages from Gmail inbox",
        new Dictionary<string, string> { ["query"] = "string", ["maxResults"] = "number" });

    public static readonly ModelTool GmailDraftTool = new(
        "gmail_create_draft",
        "Create a Gmail draft reply (partial write, does not send)",
        new Dictionary<string, string> { ["to"] = "string", ["subject"] = "string", ["body"] = "string" });

    public static IReadOnlyList<string> RequiredGoogleScopes =>
    [
        "https://www.googleapis.com/auth/gmail.readonly",
        "https://www.googleapis.com/auth/gmail.compose"
    ];

    public GmailModule(IModelClient? modelClient = null)
    {
        _modelClient = modelClient;
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        if (_modelClient is null) yield break;
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null || !IsGmailRequest(request.Text))
            yield break;

        yield return new Proposal(
            id: "gmail.read-draft",
            cons: [new ConstantValue(0.75)],
            act: async ct =>
            {
                var response = await _modelClient.GenerateAsync(
                    new ModelRequest
                    {
                        Prompt = request.Text,
                        SystemMessage = "You are a Gmail assistant. You may read Gmail messages and create draft replies only. Never send messages directly.",
                        MaxTokens = 512,
                        Tools = [GmailReadTool, GmailDraftTool]
                    },
                    ct);
                rt.Bus.Publish(new AiResponse(response.Text));
            })
        { Description = "Read Gmail messages and optionally draft a reply" };
    }

    private static bool IsGmailRequest(string text)
        => text.Contains("gmail", StringComparison.OrdinalIgnoreCase)
           || text.Contains("email inbox", StringComparison.OrdinalIgnoreCase)
           || text.Contains("email draft", StringComparison.OrdinalIgnoreCase)
           || text.Contains("email reply", StringComparison.OrdinalIgnoreCase);
}
