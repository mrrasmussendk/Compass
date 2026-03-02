using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Memory;
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
    private const int DefaultEstimatedSteps = 1;
    private static readonly string GoalList = string.Join("|", Enum.GetNames<GoalTag>());
    private readonly IModelClient? _modelClient;
    private readonly IMemoryStore? _memoryStore;

    /// <summary>
    /// Creates a goal router without an injected model client.
    /// Classification will fall back to the default goal.
    /// </summary>
    public GoalRouterSensor() { }

    /// <summary>
    /// Creates a goal router that classifies user intent with a model client.
    /// </summary>
    /// <param name="modelClient">Model client used for goal classification.</param>
    /// <param name="memoryStore">Optional memory store for conversation context.</param>
    public GoalRouterSensor(IModelClient modelClient, IMemoryStore? memoryStore = null)
    {
        _modelClient = modelClient;
        _memoryStore = memoryStore;
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

        var llmIntent = await ClassifyWithModelAsync(request.Text, activeWorkflow, recentStep, ct);
        if (llmIntent is { } match)
        {
            rt.Bus.Publish(new GoalSelected(match.Goal, match.Confidence, "llm"));
            if (match.IsCompound)
                rt.Bus.Publish(new MultiStepRequest(request.Text, match.EstimatedSteps, IsCompound: true));
            return;
        }

        // Fallback compound/multi-step detection when model classification is unavailable.
        var multiStepInfo = DetectMultiStepRequest(request.Text);
        if (multiStepInfo is not null)
            rt.Bus.Publish(multiStepInfo);

        rt.Bus.Publish(new GoalSelected(GoalTag.Answer, 0.5, "default"));
    }

    /// <summary>
    /// Attempts to classify a request into a <see cref="GoalTag"/> using the configured model.
    /// </summary>
    /// <param name="requestText">Incoming user request text.</param>
    /// <param name="activeWorkflow">Optional active workflow context.</param>
    /// <param name="recentStep">Optional recent step result context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The classified intent, or <see langword="null"/> when classification fails.</returns>
    private async Task<IntentClassification?> ClassifyWithModelAsync(
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

        // Retrieve recent conversation context if available
        var conversationContext = "none";
        if (_memoryStore is not null)
        {
            try
            {
                var history = await _memoryStore.RecallAsync<ConversationTurn>(
                    new MemoryQuery { MaxResults = 3, SortOrder = SortOrder.NewestFirst },
                    ct);

                if (history.Count > 0)
                {
                    // Only take the most recent turn (the one immediately before current request)
                    // This is most relevant for understanding number/option references
                    var mostRecentTurn = history.First();
                    
                    // Token-efficient: Truncate user message, keep assistant response focused
                    var userMsg = mostRecentTurn.Fact.UserMessage.Length > 50 
                        ? mostRecentTurn.Fact.UserMessage.Substring(0, 50) + "..." 
                        : mostRecentTurn.Fact.UserMessage;
                    
                    // Keep last 300 chars of assistant response (where numbered options usually are)
                    var assistantMsg = mostRecentTurn.Fact.AssistantResponse;
                    if (assistantMsg.Length > 300)
                    {
                        assistantMsg = "..." + assistantMsg.Substring(assistantMsg.Length - 300);
                    }
                    
                    conversationContext = $"prev_U:{userMsg}|prev_A:{assistantMsg}";
                }
            }
            catch (Exception)
            {
                // If memory retrieval fails, continue with no context
                conversationContext = "none";
            }
        }

        var response = await _modelClient.GenerateAsync(
            new ModelRequest
            {
                SystemMessage = $"Classify intent. Goals: {GoalList}. Return JSON: {{\"goal\":\"<goal>\",\"confidence\":0-1,\"isCompound\":true|false,\"estimatedSteps\":1-5}}. If user replies with number/word and conversation_history shows options, it's Answer (conf>0.9).",
                Prompt = $"req:{requestText}\nhist:{conversationContext}\nwf:{workflowContext}\nstep:{stepContext}\nvars:{variableContext}",
                MaxTokens = 64
            },
            ct);

        if (!TryParseIntentResponse(response.Text, out var intent))
            return null;

        return intent;
    }

    /// <summary>
    /// Detects if the user request contains multiple sequential actions requiring multi-step execution.
    /// </summary>
    /// <param name="requestText">The user request text to analyze.</param>
    /// <returns>A MultiStepRequest fact if compound request detected; otherwise null.</returns>
    private static MultiStepRequest? DetectMultiStepRequest(string requestText)
    {
        var lowerText = requestText.ToLowerInvariant();
        if (CompoundRequestHeuristics.IsAdvicePrompt(requestText))
            return null;

        // Check for compound request indicators
        var compoundIndicators = new[]
        {
            " then ",
            " and then ",
            " afterwards ",
            " after that ",
            " next ",
            " followed by ",
            " after ",
        };

        var hasCompoundIndicator = compoundIndicators.Any(indicator => lowerText.Contains(indicator));

        // Also check for multiple action verbs indicating compound request
        var actionVerbs = new[] { "create", "write", "read", "delete", "update", "insert", "add", "remove", "modify", "input" };
        var verbCount = actionVerbs.Count(verb => lowerText.Contains(verb));
        var hasMultipleActions = verbCount >= 2;

        if (!hasCompoundIndicator && !hasMultipleActions)
            return null;

        // Estimate number of steps
        int stepCount = 1;
        if (hasCompoundIndicator)
            stepCount = 1 + compoundIndicators.Count(indicator => lowerText.Contains(indicator));
        else if (hasMultipleActions)
            stepCount = verbCount;

        return new MultiStepRequest(
            OriginalRequest: requestText,
            EstimatedSteps: Math.Min(stepCount, 5), // Cap at 5 to prevent excessive execution
            IsCompound: true
        );
    }

    /// <summary>
    /// Parses model output JSON into a typed intent classification.
    /// </summary>
    /// <param name="text">Raw model output text.</param>
    /// <param name="intent">Parsed intent values when successful.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    private static bool TryParseIntentResponse(string text, out IntentClassification intent)
    {
        intent = new IntentClassification(GoalTag.Answer, DefaultModelConfidence, false, DefaultEstimatedSteps);

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (!root.TryGetProperty("goal", out var goalElement))
                return false;

            string? goalName = goalElement.GetString();
            if (string.IsNullOrWhiteSpace(goalName))
                return false;

            if (!Enum.TryParse(goalName, ignoreCase: true, out GoalTag goal))
                return false;

            var confidence = DefaultModelConfidence;
            if (root.TryGetProperty("confidence", out var confidenceElement) &&
                confidenceElement.ValueKind is JsonValueKind.Number &&
                confidenceElement.TryGetDouble(out var parsed))
            {
                confidence = Math.Clamp(parsed, 0, 1);
            }

            var isCompound = false;
            if (root.TryGetProperty("isCompound", out var isCompoundElement)
                && isCompoundElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                isCompound = isCompoundElement.GetBoolean();
            }

            var estimatedSteps = root.TryGetProperty("estimatedSteps", out var estimatedStepsElement)
                && estimatedStepsElement.ValueKind == JsonValueKind.Number
                && estimatedStepsElement.TryGetInt32(out var parsedSteps)
                ? Math.Clamp(parsedSteps, 1, 5)
                : DefaultEstimatedSteps;

            intent = new IntentClassification(goal, confidence, isCompound, estimatedSteps);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record IntentClassification(GoalTag Goal, double Confidence, bool IsCompound, int EstimatedSteps);
}
