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
    private const double DefaultModelConfidence = 0.7;
    private static readonly string GoalList = string.Join("|", Enum.GetNames<GoalTag>());
    private readonly IModelClient? _modelClient;

    /// <summary>
    /// Creates a goal router without an injected model client.
    /// Classification will fall back to the default goal.
    /// </summary>
    public GoalRouterSensor() { }

    /// <summary>
    /// Creates a goal router that classifies user intent with a model client.
    /// </summary>
    /// <param name="modelClient">Model client used for goal classification.</param>
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
        var recentStep = rt.Bus.GetOrDefault<StepResult>();
        var llmGoal = await ClassifyWithModelAsync(request.Text, activeWorkflow, recentStep, ct);
        if (llmGoal is { } match)
        {
            rt.Bus.Publish(new GoalSelected(match.Goal, match.Confidence, "llm"));
            return;
        }

        rt.Bus.Publish(new GoalSelected(GoalTag.Answer, 0.5, "default"));
    }

    /// <summary>
    /// Attempts to classify a request into a <see cref="GoalTag"/> using the configured model.
    /// </summary>
    /// <param name="requestText">Incoming user request text.</param>
    /// <param name="activeWorkflow">Optional active workflow context.</param>
    /// <param name="recentStep">Optional recent step result context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The classified goal/confidence tuple, or <see langword="null"/> when classification fails.</returns>
    private async Task<(GoalTag Goal, double Confidence)?> ClassifyWithModelAsync(
        string requestText,
        ActiveWorkflow? activeWorkflow,
        StepResult? recentStep,
        CancellationToken ct)
    {
        if (_modelClient is null)
            return null;

        var workflowContext = activeWorkflow is null
            ? "none"
            : $"{activeWorkflow.WorkflowId} ({activeWorkflow.Status})";
        var stepContext = recentStep is null
            ? "none"
            : $"{recentStep.Outcome}: {recentStep.Message ?? "no-message"}";
        var variableContext = recentStep?.OutputFacts is { Count: > 0 }
            ? string.Join("|", recentStep.OutputFacts.Keys)
            : "none";

        var response = await _modelClient.GenerateAsync(
            new ModelRequest
            {
                SystemMessage = $"You classify request intent for UtilityAi Compass goal routing.\n"
                    + $"Return strict JSON: {{\"goal\":\"{GoalList}\",\"confidence\":0..1}}.",
                Prompt = $"request: {requestText}\nactive_workflow: {workflowContext}\nrecent_step: {stepContext}\nset_variables: {variableContext}",
                MaxTokens = 64
            },
            ct);

        if (!TryParseGoalResponse(response.Text, out var goal, out var confidence))
            return null;

        return (goal, confidence);
    }

    /// <summary>
    /// Parses model output JSON into a typed goal classification.
    /// </summary>
    /// <param name="text">Raw model output text.</param>
    /// <param name="goal">Parsed goal value when successful.</param>
    /// <param name="confidence">Parsed confidence value when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
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

            string? goalName = goalElement.GetString();
            if (string.IsNullOrWhiteSpace(goalName))
                return false;

            if (!Enum.TryParse(goalName, ignoreCase: true, out goal))
                return false;

            if (root.TryGetProperty("confidence", out var confidenceElement) &&
                confidenceElement.ValueKind is JsonValueKind.Number &&
                confidenceElement.TryGetDouble(out var parsed))
            {
                confidence = Math.Clamp(parsed, 0, 1);
            }
            else
            {
                confidence = DefaultModelConfidence;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
