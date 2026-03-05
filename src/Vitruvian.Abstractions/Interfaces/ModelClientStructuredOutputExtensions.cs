using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VitruvianAbstractions.Interfaces;

/// <summary>
/// Extensions for requesting and parsing structured JSON output from model clients.
/// </summary>
public static class ModelClientStructuredOutputExtensions
{
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T> GenerateStructuredAsync<T>(
        this IModelClient modelClient,
        string prompt,
        string? systemMessage = null,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(modelClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var schema = BuildSchema(typeof(T));
        var schemaJson = schema.ToJsonString();
        var structuredInstructions =
            """
            Return only valid JSON that matches this JSON Schema exactly.
            Do not include markdown, code fences, or extra text.
            JSON Schema:
            """
            + "\n" + schemaJson;

        var request = new ModelRequest
        {
            Prompt = prompt,
            SystemMessage = string.IsNullOrWhiteSpace(systemMessage)
                ? structuredInstructions
                : systemMessage + "\n\n" + structuredInstructions
        };

        var response = await modelClient.GenerateAsync(request, cancellationToken);
        return ParseStructuredResult<T>(response.Text, serializerOptions ?? DefaultSerializerOptions);
    }

    private static T ParseStructuredResult<T>(string rawText, JsonSerializerOptions serializerOptions) where T : class
    {
        if (string.IsNullOrWhiteSpace(rawText))
            throw new InvalidOperationException("Model returned empty structured output.");

        var normalized = rawText.Trim();
        if (normalized.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = normalized.IndexOf('\n');
            if (firstLineEnd >= 0)
            {
                normalized = normalized[(firstLineEnd + 1)..];
            }

            var closingFence = normalized.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                normalized = normalized[..closingFence];
            }

            normalized = normalized.Trim();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<T>(normalized, serializerOptions);
            return parsed ?? throw new InvalidOperationException($"Structured output deserialized to null for type '{typeof(T).Name}'.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse structured output as '{typeof(T).Name}'.", ex);
        }
    }

    private static JsonObject BuildSchema(Type type, int depth = 0)
    {
        if (depth > 8)
            return new JsonObject { ["type"] = "object", ["additionalProperties"] = false };

        if (type.IsEnum)
        {
            return new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray(Enum.GetNames(type).Select(static value => (JsonNode?)value).ToArray())
            };
        }

        if (type == typeof(string) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateOnly) || type == typeof(TimeOnly))
            return new JsonObject { ["type"] = "string" };

        if (type == typeof(bool))
            return new JsonObject { ["type"] = "boolean" };

        if (type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long))
            return new JsonObject { ["type"] = "integer" };

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return new JsonObject { ["type"] = "number" };

        if (TryGetArrayItemType(type, out var itemType))
        {
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = BuildSchema(itemType, depth + 1)
            };
        }

        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead)
                continue;

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            properties[property.Name] = BuildSchema(propertyType, depth + 1);
            required.Add(property.Name);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    private static bool TryGetArrayItemType(Type type, out Type itemType)
    {
        itemType = default!;

        if (type.IsArray)
        {
            itemType = type.GetElementType()!;
            return true;
        }

        if (type.IsGenericType &&
            (type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
             type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) ||
             type.GetGenericTypeDefinition() == typeof(IList<>) ||
             type.GetGenericTypeDefinition() == typeof(List<>)))
        {
            itemType = type.GetGenericArguments()[0];
            return true;
        }

        return false;
    }
}
