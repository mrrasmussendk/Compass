using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.Runtime.Sensors;

/// <summary>
/// Detects read/write/update intent from user input using keyword heuristics
/// and publishes a <see cref="CliIntent"/> fact to the EventBus.
/// </summary>
public sealed class CliIntentSensor : ISensor
{
    private static readonly (string[] Keywords, CliVerb Verb, double Confidence)[] Rules =
    [
        (["read", "get", "show", "list", "view", "fetch", "display", "print"], CliVerb.Read, 0.90),
        (["update", "edit", "modify", "change", "patch", "alter", "replace"], CliVerb.Update, 0.90),
        (["write", "create", "add", "new", "insert", "set ", "store", "save"], CliVerb.Write, 0.90),
    ];

    public Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        if (rt.Bus.TryGet<CliIntent>(out _)) return Task.CompletedTask;

        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) return Task.CompletedTask;

        var text = request.Text.ToLowerInvariant().Trim();

        foreach (var (keywords, verb, confidence) in Rules)
        {
            if (keywords.Any(k => text.Contains(k)))
            {
                var target = ExtractTarget(text, keywords);
                rt.Bus.Publish(new CliIntent(verb, target, confidence));
                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }

    internal static string? ExtractTarget(string text, string[] matchedKeywords)
    {
        var keyword = matchedKeywords.FirstOrDefault(k => text.Contains(k));
        if (keyword is null) return null;

        var idx = text.IndexOf(keyword, StringComparison.Ordinal);
        var remainder = text[(idx + keyword.Length)..].Trim();

        var words = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 ? words[0] : null;
    }
}
