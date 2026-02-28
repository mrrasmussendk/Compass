using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Sensor;
using System.Text.RegularExpressions;

namespace UtilityAi.Compass.Runtime.Sensors;

/// <summary>
/// Keyword-based sensor that detects user intent from <see cref="UserRequest"/> text
/// and publishes a <see cref="GoalSelected"/> fact to the EventBus.
/// </summary>
public sealed class GoalRouterSensor : ISensor
{
    /// <summary>Heuristic keyword rules mapping text patterns to <see cref="GoalTag"/> values with confidence scores.</summary>
    private static readonly (string[] Keywords, GoalTag Goal, double Confidence)[] Rules =
    [
        (["stop", "cancel", "abort", "quit", "halt"], GoalTag.Stop, 0.95),
        (["approve", "confirm", "accept", "yes, proceed", "granted"], GoalTag.Approve, 0.90),
        (["summarize", "summary", "tldr", "tl;dr", "brief"], GoalTag.Summarize, 0.85),
        (["run", "execute", "do", "perform", "apply", "deploy", "create", "write", "make"], GoalTag.Execute, 0.80),
        (["clarify", "what do you mean", "explain", "rephrase"], GoalTag.Clarify, 0.80),
        (["?", "how", "what", "why", "when", "who", "where"], GoalTag.Answer, 0.70),
    ];

    /// <inheritdoc />
    public Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        if (rt.Bus.TryGet<GoalSelected>(out var existing) && existing.Confidence >= 0.85)
            return Task.CompletedTask;

        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) return Task.CompletedTask;

        var text = request.Text.ToLowerInvariant();

        var bestMatch = Rules
            .SelectMany(rule => rule.Keywords, (rule, keyword) => (rule.Goal, rule.Confidence, keyword))
            .Where(match => IsKeywordMatch(text, match.keyword))
            .OrderByDescending(match => match.Confidence)
            .FirstOrDefault();

        if (bestMatch.keyword is not null)
        {
            rt.Bus.Publish(new GoalSelected(bestMatch.Goal, bestMatch.Confidence, "heuristic"));
            return Task.CompletedTask;
        }

        rt.Bus.Publish(new GoalSelected(GoalTag.Answer, 0.5, "default"));
        return Task.CompletedTask;
    }

    private static bool IsKeywordMatch(string text, string keyword)
    {
        if (keyword == "?")
            return text.Contains('?');

        return Regex.IsMatch(text, $@"(?<!\w){Regex.Escape(keyword)}(?!\w)");
    }
}
