using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;
using UtilityAi.Utils;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Standard module that performs a web search by delegating to the
/// host-provided <see cref="IModelClient"/>. Declares a
/// <c>web_search</c> tool so the host can enable provider-native
/// web search (e.g. GPT-5.2 <c>ResponseTool.CreateWebSearchTool()</c>).
/// </summary>
[CompassCapability("web-search", priority: 3)]
[CompassGoals(GoalTag.Answer)]
[CompassLane(Lane.Execute)]
[CompassCost(0.4)]
[CompassRisk(0.1)]
[CompassCooldown("web-search.query", secondsTtl: 10)]
public sealed class WebSearchModule : ICapabilityModule
{
    private readonly IModelClient? _modelClient;

    /// <summary>Web search tool declared for the model request.</summary>
    public static readonly ModelTool WebSearchTool = new(
        "web_search",
        "Search the web for real-time information",
        new Dictionary<string, string> { ["query"] = "string" });

    public WebSearchModule(IModelClient? modelClient = null)
    {
        _modelClient = modelClient;
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        if (_modelClient is null) yield break;
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
                        SystemMessage = "You are a web search assistant. Provide a concise, factual answer to the user's query with sources.",
                        Temperature = 0.3,
                        MaxTokens = 512,
                        Tools = [WebSearchTool]
                    },
                    ct);
                rt.Bus.Publish(new AiResponse(response.Text));
            }
        ) { Description = "Search the web for an answer" };
    }

    /// <summary>
    /// Builds a structured API request JSON using <see cref="AiRequestBuilder"/>
    /// with the <c>web_search_preview</c> tool and <see cref="WebSearchResult"/> schema.
    /// Hosts can use this to make direct provider API calls with structured output.
    /// </summary>
    /// <param name="model">Model identifier (e.g. "gpt-5.2").</param>
    /// <param name="userPrompt">The user's search query.</param>
    public static string BuildStructuredRequest(string model, string userPrompt)
    {
        return AiRequestBuilder.Create()
            .WithModel(model)
            .AddTool("web_search_preview")
            .AddSystem("You are a web search assistant. Provide a concise, factual answer to the user's query with sources.")
            .AddUser(userPrompt)
            .WithJsonSchemaFrom<WebSearchResult>("web_search_result")
            .BuildJson();
    }
}
