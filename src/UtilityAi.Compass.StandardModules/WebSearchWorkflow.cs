using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Abstractions.Workflow;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Workflow module that performs web searches using the host-provided <see cref="IModelClient"/>.
/// Single-step workflow: send query to model with web search tool and publish results.
/// </summary>
public sealed class WebSearchWorkflow : IWorkflowModule
{
    private readonly IModelClient _modelClient;

    public WebSearchWorkflow(IModelClient modelClient)
    {
        _modelClient = modelClient;
    }

    /// <inheritdoc />
    public WorkflowDefinition Define() => new(
        WorkflowId: "web-search",
        DisplayName: "Web Search",
        Goals: [GoalTag.Answer],
        Lanes: [Lane.Execute],
        Steps:
        [
            new StepDefinition(
                StepId: "query",
                DisplayName: "Search the web",
                RequiresFacts: ["UserRequest"],
                ProducesFacts: ["AiResponse"],
                Idempotent: true,
                MaxRetries: 2,
                Timeout: TimeSpan.FromSeconds(30))
        ],
        CanInterrupt: true,
        EstimatedCost: 0.4,
        RiskLevel: 0.1);

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeStart(UtilityAi.Utils.Runtime rt)
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
                        SystemMessage = "You are a web search assistant. Provide a concise, factual answer to the user's query with sources.",
                        Temperature = 0.3,
                        MaxTokens = 512,
                        Tools = [WebSearchModule.WebSearchTool]
                    },
                    ct);
                rt.Bus.Publish(new AiResponse(response.Text));
                rt.Bus.Publish(new StepResult(StepOutcome.Succeeded, "Web search completed"));
            })
        { Description = "Search the web for an answer" };
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeSteps(UtilityAi.Utils.Runtime rt, ActiveWorkflow active) =>
        [];

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeRepair(UtilityAi.Utils.Runtime rt, ActiveWorkflow active, RepairDirective directive) =>
        [];
}
