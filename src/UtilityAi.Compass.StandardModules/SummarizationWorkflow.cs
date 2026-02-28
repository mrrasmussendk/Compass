using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Abstractions.Workflow;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Workflow module that summarizes content using the host-provided <see cref="IModelClient"/>.
/// Single-step workflow: send prompt to model and publish the summary.
/// </summary>
public sealed class SummarizationWorkflow : IWorkflowModule
{
    private readonly IModelClient? _modelClient;

    public SummarizationWorkflow(IModelClient? modelClient = null)
    {
        _modelClient = modelClient;
    }

    /// <inheritdoc />
    public WorkflowDefinition Define() => new(
        WorkflowId: "summarization",
        DisplayName: "Summarize Content",
        Goals: [GoalTag.Summarize],
        Lanes: [Lane.Communicate],
        Steps:
        [
            new StepDefinition(
                StepId: "summarize",
                DisplayName: "Generate summary via LLM",
                RequiresFacts: ["UserRequest"],
                ProducesFacts: ["AiResponse"],
                Idempotent: true,
                MaxRetries: 2,
                Timeout: TimeSpan.FromSeconds(30))
        ],
        CanInterrupt: true,
        EstimatedCost: 0.3,
        RiskLevel: 0.0);

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeStart(UtilityAi.Utils.Runtime rt)
    {
        if (_modelClient is null) yield break;
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
                        SystemMessage = "You are a summarization assistant. Provide a clear and concise summary of the content the user provides, including key points.",
                        Temperature = 0.3,
                        MaxTokens = 256
                    },
                    ct);
                rt.Bus.Publish(new AiResponse(response.Text));
                rt.Bus.Publish(new StepResult(StepOutcome.Succeeded, "Summary generated"));
            })
        { Description = "Summarize the provided content" };
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeSteps(UtilityAi.Utils.Runtime rt, ActiveWorkflow active) =>
        [];

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeRepair(UtilityAi.Utils.Runtime rt, ActiveWorkflow active, RepairDirective directive) =>
        [];
}
