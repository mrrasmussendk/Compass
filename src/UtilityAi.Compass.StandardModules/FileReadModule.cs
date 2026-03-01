using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Standard module that reads file contents from disk and publishes
/// the result as an <see cref="AiResponse"/>.
/// </summary>
[CompassCapability("file-read", priority: 3)]
[CompassGoals(GoalTag.Answer)]
[CompassLane(Lane.Execute)]
[CompassCost(0.1)]
[CompassRisk(0.0)]
[CompassSideEffects(SideEffectLevel.ReadOnly)]
public sealed class FileReadModule : ICapabilityModule
{
    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        var path = ExtractFilePath(request.Text);
        
        // Only propose if the request looks like a file read operation
        var looksLikeFileRequest = 
            request.Text.Contains("read", StringComparison.OrdinalIgnoreCase) ||
            request.Text.Contains("show", StringComparison.OrdinalIgnoreCase) ||
            request.Text.Contains("open", StringComparison.OrdinalIgnoreCase) ||
            request.Text.Contains("cat", StringComparison.OrdinalIgnoreCase) ||
            request.Text.Contains("file", StringComparison.OrdinalIgnoreCase) ||
            (path.Contains('.') || path.Contains(Path.DirectorySeparatorChar) || 
             path.Contains(Path.AltDirectorySeparatorChar));

        if (!looksLikeFileRequest) yield break;

        yield return new Proposal(
            id: "file-read.read",
            cons: [new ConstantValue(0.7)],
            act: _ =>
            {
                if (!File.Exists(path))
                {
                    rt.Bus.Publish(new AiResponse($"File not found: {path}"));
                    return Task.CompletedTask;
                }

                var content = File.ReadAllText(path);
                rt.Bus.Publish(new AiResponse(content));
                return Task.CompletedTask;
            }
        ) { Description = "Read a file and return its contents" };
    }

    public static string ExtractFilePath(string text)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Return the last token that looks like a file path
        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            if (tokens[i].Contains('.') || tokens[i].Contains(Path.DirectorySeparatorChar) ||
                tokens[i].Contains(Path.AltDirectorySeparatorChar))
                return tokens[i];
        }

        return tokens.Length > 0 ? tokens[^1] : string.Empty;
    }
}
