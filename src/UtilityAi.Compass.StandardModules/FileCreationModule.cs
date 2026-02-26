using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Standard module that creates a file on disk.
/// Responds to execute-intent requests containing a file path and content.
/// </summary>
[CompassCapability("file-creation", priority: 3)]
[CompassGoals(GoalTag.Execute)]
[CompassLane(Lane.Execute)]
[CompassCost(0.2)]
[CompassRisk(0.3)]
[CompassSideEffects(SideEffectLevel.Write)]
[CompassCooldown("file-creation.write", secondsTtl: 5)]
public sealed class FileCreationModule : ICapabilityModule
{
    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        yield return new Proposal(
            id: "file-creation.write",
            cons: [new ConstantValue(0.75)],
            act: _ =>
            {
                var (path, content) = ParseFileRequest(request.Text);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(path, content);
                rt.Bus.Publish(new AiResponse($"File created: {path}"));
                return Task.CompletedTask;
            }
        ) { Description = "Create a file with the specified content" };
    }

    public static (string Path, string Content) ParseFileRequest(string text)
    {
        // Expected patterns:
        //   "create file <path> with content <content>"
        //   "write file <path> <content>"
        var lower = text.ToLowerInvariant();

        var withContent = lower.IndexOf(" with content ", StringComparison.Ordinal);
        if (withContent >= 0)
        {
            var before = text[..withContent];
            var content = text[(withContent + " with content ".Length)..];

            var pathToken = ExtractPathToken(before);
            return (pathToken, content);
        }

        // Fallback: treat last token as path, rest as content
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length >= 3)
        {
            var path = tokens[^1];
            return (path, string.Join(' ', tokens[2..^1]));
        }

        return ("output.txt", text);
    }

    private static string ExtractPathToken(string segment)
    {
        var tokens = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 0 ? tokens[^1] : "output.txt";
    }
}
