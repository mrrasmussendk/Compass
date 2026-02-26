using UtilityAi.Nexus.Abstractions;
using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Sensor;

namespace UtilityAi.Nexus.Runtime.Sensors;

public sealed class GoalRouterSensor : ISensor
{
    private static readonly (string[] Keywords, GoalTag Goal, double Confidence)[] Rules =
    [
        (["stop", "cancel", "abort", "quit", "halt"], GoalTag.Stop, 0.95),
        (["approve", "confirm", "accept", "yes, proceed", "granted"], GoalTag.Approve, 0.90),
        (["summarize", "summary", "tldr", "tl;dr", "brief"], GoalTag.Summarize, 0.85),
        (["run", "execute", "do ", "perform", "apply", "deploy", "read", "write", "update"], GoalTag.Execute, 0.80),
        (["clarify", "what do you mean", "explain", "rephrase"], GoalTag.Clarify, 0.80),
        (["?", "how ", "what ", "why ", "when ", "who ", "where "], GoalTag.Answer, 0.70),
    ];

    public Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        if (rt.Bus.TryGet<GoalSelected>(out var existing) && existing.Confidence >= 0.85)
            return Task.CompletedTask;

        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) return Task.CompletedTask;

        var text = request.Text.ToLowerInvariant();

        foreach (var (keywords, goal, confidence) in Rules)
        {
            if (keywords.Any(k => text.Contains(k)))
            {
                rt.Bus.Publish(new GoalSelected(goal, confidence, "heuristic"));
                return Task.CompletedTask;
            }
        }

        rt.Bus.Publish(new GoalSelected(GoalTag.Answer, 0.5, "default"));
        return Task.CompletedTask;
    }
}
