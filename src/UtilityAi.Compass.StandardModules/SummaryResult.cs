using System.ComponentModel;
using System.Text.Json.Serialization;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Structured output DTO for summarization results.
/// Used with <see cref="UtilityAi.Helpers.OpenAiStructuredOutputHelper.AiRequestBuilder"/>
/// and <see cref="UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator.JsonSchemaGenerator"/>
/// to request typed responses from the model.
/// </summary>
public sealed record SummaryResult
{
    /// <summary>A concise summary of the provided content.</summary>
    [JsonPropertyName("summary")]
    [Description("A concise summary of the provided content.")]
    public required string Summary { get; init; }

    /// <summary>Key points extracted from the content.</summary>
    [JsonPropertyName("keyPoints")]
    [Description("Key points extracted from the content.")]
    public required string[] KeyPoints { get; init; }
}
