using System.Text.Json;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Module that handles compound requests intelligently using the LLM.
/// When a compound request is detected (via <see cref="MultiStepRequest"/>),
/// this module uses the model client to understand the full request,
/// execute file operations, and answer questions in a single response.
/// Falls back to guidance when no model client is available.
/// </summary>
[CompassCapability("compound-request-handler", priority: 8)]
[CompassGoals(GoalTag.Execute, GoalTag.Answer)]
[CompassLane(Lane.Execute)]
[CompassCost(0.2)]
[CompassRisk(0.1)]
public sealed class CompoundRequestModule : ICapabilityModule
{
    private readonly IModelClient? _modelClient;

    /// <summary>
    /// Creates a compound request handler.
    /// </summary>
    /// <param name="modelClient">Optional model client for intelligent compound request handling.</param>
    public CompoundRequestModule(IModelClient? modelClient = null)
    {
        _modelClient = modelClient;
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var multiStep = rt.Bus.GetOrDefault<MultiStepRequest>();
        if (multiStep is null || !multiStep.IsCompound)
            yield break;

        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null)
            yield break;

        if (_modelClient is null)
        {
            yield return new Proposal(
                id: "compound-request.respond",
                cons: [new ConstantValue(0.85)],
                act: _ =>
                {
                    rt.Bus.Publish(new AiResponse(
                        "I detected multiple tasks in your request. " +
                        "For best results, please submit each task as a separate request.\n\n" +
                        "This ensures each task gets proper attention and the results are clear."));
                    return Task.CompletedTask;
                }
            ) { Description = "Respond to compound request with guidance" };
            yield break;
        }

        var modelClient = _modelClient;
        yield return new Proposal(
            id: "compound-request.handle",
            cons: [new ConstantValue(0.85)],
            act: async ct =>
            {
                // Step 1: Use LLM to extract file operations from the request
                var fileOps = await ExtractFileOperationsAsync(modelClient, request.Text, ct);

                // Step 2: Execute any detected file operations
                var fileResults = ExecuteFileOperations(fileOps);

                // Step 3: Use LLM to generate a conversational response for all parts
                var contextForLlm = fileResults.Count > 0
                    ? $"I've already performed these file operations:\n{string.Join("\n", fileResults)}\n\nNow address any remaining parts of the user's request: {request.Text}"
                    : request.Text;

                var conversationalResponse = await modelClient.GenerateAsync(
                    new ModelRequest
                    {
                        SystemMessage = "You are a helpful AI assistant. The user made a compound request. " +
                                        "Some parts may have already been handled (file operations). " +
                                        "Acknowledge completed actions briefly and address any remaining questions or requests naturally.",
                        Prompt = contextForLlm,
                        MaxTokens = 512
                    }, ct);

                // Combine file operation results with the conversational response
                var finalResponse = fileResults.Count > 0
                    ? string.Join("\n", fileResults) + "\n\n" + conversationalResponse.Text
                    : conversationalResponse.Text;

                rt.Bus.Publish(new AiResponse(finalResponse));
            }
        ) { Description = "Handle compound request with file operations and conversation" };
    }

    /// <summary>
    /// Uses the LLM to extract file operations from a compound request.
    /// Falls back to the existing <see cref="FileCreationModule.ParseFileRequest"/>
    /// parser if the LLM response cannot be parsed.
    /// </summary>
    internal static async Task<List<(string Filename, string Content)>> ExtractFileOperationsAsync(
        IModelClient modelClient, string requestText, CancellationToken ct)
    {
        var result = new List<(string Filename, string Content)>();

        try
        {
            var response = await modelClient.GenerateAsync(
                new ModelRequest
                {
                    SystemMessage = "Extract file operations from the user's request. " +
                                    "Return a JSON array of objects with \"filename\" and \"content\" properties. " +
                                    "If no file operations found, return an empty array []. " +
                                    "Only return valid JSON, nothing else.",
                    Prompt = requestText,
                    MaxTokens = 256
                }, ct);

            using var doc = JsonDocument.Parse(response.Text);
            var array = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement
                : doc.RootElement.TryGetProperty("file_ops", out var fileOps) ? fileOps : default;

            if (array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    var filename = item.TryGetProperty("filename", out var fn) ? fn.GetString() : null;
                    var content = item.TryGetProperty("content", out var contentElement) ? contentElement.GetString() : null;
                    if (!string.IsNullOrEmpty(filename) && content is not null)
                        result.Add((filename, content));
                }
            }
        }
        catch
        {
            // LLM response wasn't valid JSON; fall back to the existing regex-based parser.
            // Silently handling the exception is acceptable here because the LLM output format
            // is not guaranteed, and the fallback parser provides a best-effort extraction.
            try
            {
                var (path, content) = FileCreationModule.ParseFileRequest(requestText);
                // "output.txt" is the default fallback filename from ParseFileRequest
                // when no path could be extracted - skip it to avoid creating unintended files.
                if (!string.IsNullOrEmpty(path) && path != "output.txt")
                    result.Add((path, content));
            }
            catch
            {
                // Fallback parser also failed; no file operations could be extracted.
            }
        }

        return result;
    }

    /// <summary>
    /// Executes a list of file operations, returning human-readable result messages.
    /// Only relative paths without directory traversal are allowed; absolute paths and
    /// paths containing ".." are rejected to prevent writing to unintended locations.
    /// </summary>
    public static List<string> ExecuteFileOperations(List<(string Filename, string Content)> fileOps)
    {
        var results = new List<string>();
        foreach (var (filename, content) in fileOps)
        {
            try
            {
                if (Path.IsPathRooted(filename) || filename.Contains(".."))
                {
                    results.Add($"Skipped '{filename}': only relative paths without directory traversal are allowed.");
                    continue;
                }

                var directory = Path.GetDirectoryName(filename);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(filename, content);
                results.Add($"File '{filename}' created with content: \"{content}\"");
            }
            catch (Exception ex)
            {
                results.Add($"Failed to create '{filename}': {ex.Message}");
            }
        }
        return results;
    }
}
