using System.ComponentModel;
using System.Text.Json.Serialization;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Structured output DTO for web search results.
/// Used with <see cref="UtilityAi.Helpers.OpenAiStructuredOutputHelper.AiRequestBuilder"/>
/// and <see cref="UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator.JsonSchemaGenerator"/>
/// to request typed responses from the model.
/// </summary>
public sealed record WebSearchResult
{
    /// <summary>A concise answer derived from web search results.</summary>
    [JsonPropertyName("answer")]
    [Description("A concise answer derived from web search results.")]
    public required string Answer { get; init; }

    /// <summary>Source URLs that support the answer.</summary>
    [JsonPropertyName("sources")]
    [Description("Source URLs that support the answer.")]
    public required string[] Sources { get; init; }
}
