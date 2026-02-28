using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Sensor;
using System.Text.Json;

namespace UtilityAi.Compass.Runtime.Sensors;

/// <summary>
/// LLM-based sensor that detects user intent from <see cref="UserRequest"/> text
/// and publishes a <see cref="GoalSelected"/> fact to the EventBus.
/// </summary>
public sealed class GoalRouterSensor : ISensor
{
    private readonly IModelClient? _modelClient;

    public GoalRouterSensor() { }

    public GoalRouterSensor(IModelClient modelClient)
    {
        _modelClient = modelClient;
    }

    /// <inheritdoc />
    public async Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        if (rt.Bus.TryGet<GoalSelected>(out var existing) && existing.Confidence >= 0.85)
            return;

        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null)
            return;

        var activeWorkflow = rt.Bus.GetOrDefault<ActiveWorkflow>();
        var llmGoal = await ClassifyWithModelAsync(request.Text, activeWorkflow, ct);
        if (llmGoal is { } match)
        {
            rt.Bus.Publish(new GoalSelected(match.Goal, match.Confidence, "llm"));
            return;
        }

        rt.Bus.Publish(new GoalSelected(GoalTag.Answer, 0.5, "default"));
    }

    private async Task<(GoalTag Goal, double Confidence)?> ClassifyWithModelAsync(
        string requestText,
        ActiveWorkflow? activeWorkflow,
        CancellationToken ct)
    {
        if (_modelClient is null)
            return null;

        var workflowContext = activeWorkflow is null
            ? "none"
            : $"{activeWorkflow.WorkflowId} ({activeWorkflow.Status})";

        var response = await _modelClient.GenerateAsync(
            new ModelRequest
            {
                SystemMessage = """
                                You classify request intent for UtilityAi Compass goal routing.
                                Return strict JSON: {"goal":"Answer|Clarify|Summarize|Execute|Approve|Stop","confidence":0..1}.
                                """,
                Prompt = $"request: {requestText}\nactive_workflow: {workflowContext}",
                Temperature = 0.0,
                MaxTokens = 64
            },
            ct);

        if (!TryParseGoalResponse(response.Text, out var goal, out var confidence))
            return null;

        return (goal, confidence);
    }

    private static bool TryParseGoalResponse(string text, out GoalTag goal, out double confidence)
    {
        goal = GoalTag.Answer;
        confidence = 0;

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (!root.TryGetProperty("goal", out var goalElement))
                return false;

            if (!Enum.TryParse(goalElement.GetString(), ignoreCase: true, out goal))
                return false;

            if (root.TryGetProperty("confidence", out var confidenceElement) &&
                confidenceElement.ValueKind is JsonValueKind.Number &&
                confidenceElement.TryGetDouble(out var parsed))
            {
                confidence = Math.Clamp(parsed, 0, 1);
            }
            else
            {
                confidence = 0.7;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
