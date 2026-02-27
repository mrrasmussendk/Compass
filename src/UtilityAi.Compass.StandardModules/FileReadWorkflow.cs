using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Abstractions.Workflow;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Workflow module that reads a file from disk.
/// Single-step workflow: parse the request, read the file, and publish the result.
/// </summary>
public sealed class FileReadWorkflow : IWorkflowModule
{
    /// <inheritdoc />
    public WorkflowDefinition Define() => new(
        WorkflowId: "file-read",
        DisplayName: "Read File",
        Goals: [GoalTag.Answer],
        Lanes: [Lane.Execute],
        Steps:
        [
            new StepDefinition(
                StepId: "read",
                DisplayName: "Read file contents",
                RequiresFacts: ["UserRequest"],
                ProducesFacts: ["AiResponse"],
                Idempotent: true,
                MaxRetries: 1)
        ],
        CanInterrupt: true,
        EstimatedCost: 0.1,
        RiskLevel: 0.0);

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeStart(UtilityAi.Utils.Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        yield return new Proposal(
            id: "file-read.read",
            cons: [new ConstantValue(0.7)],
            act: _ =>
            {
                var path = FileReadModule.ExtractFilePath(request.Text);

                if (!File.Exists(path))
                {
                    rt.Bus.Publish(new AiResponse($"File not found: {path}"));
                    rt.Bus.Publish(new StepResult(StepOutcome.Succeeded, $"File not found: {path}"));
                    return Task.CompletedTask;
                }

                var content = File.ReadAllText(path);
                rt.Bus.Publish(new AiResponse(content));
                rt.Bus.Publish(new StepResult(StepOutcome.Succeeded, "File read successfully"));
                return Task.CompletedTask;
            })
        { Description = "Read a file and return its contents" };
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeSteps(UtilityAi.Utils.Runtime rt, ActiveWorkflow active) =>
        [];

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeRepair(UtilityAi.Utils.Runtime rt, ActiveWorkflow active, RepairDirective directive) =>
        [];
}
