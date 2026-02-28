using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UtilityAi.Compass.Abstractions.Interfaces;

namespace Compass.SampleHost;

file static class IntegrationDefaults
{
    public const int DefaultModelMaxTokens = 512;
    public const int DefaultDiscordPollIntervalSeconds = 2;
    public const int DefaultDiscordMessageLimit = 25;
    public const int MaxDiscordMessageLimit = 100;

    public static async Task EnsureSuccessAsync(string provider, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"{provider} API error {(int)response.StatusCode} ({response.StatusCode}): {errorBody}");
    }
}

public enum ModelProvider
{
    OpenAi,
    Anthropic,
    Gemini
}

public sealed record ModelConfiguration(ModelProvider Provider, string ApiKey, string Model)
{
    public static bool TryCreateFromEnvironment(out ModelConfiguration? configuration)
    {
        var providerText = Environment.GetEnvironmentVariable("COMPASS_MODEL_PROVIDER");
        if (!TryParseProvider(providerText, out var provider))
        {
            configuration = null;
            return false;
        }

        var (apiKeyVariable, defaultModel) = provider switch
        {
            ModelProvider.Anthropic => ("ANTHROPIC_API_KEY", "claude-3-5-haiku-latest"),
            ModelProvider.Gemini => ("GEMINI_API_KEY", "gemini-2.0-flash"),
            _ => ("OPENAI_API_KEY", "gpt-4o-mini")
        };

        var apiKey = Environment.GetEnvironmentVariable(apiKeyVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            configuration = null;
            return false;
        }

        var model = Environment.GetEnvironmentVariable("COMPASS_MODEL_NAME");
        configuration = new ModelConfiguration(provider, apiKey, string.IsNullOrWhiteSpace(model) ? defaultModel : model);
        return true;
    }

    public static bool TryParseProvider(string? value, out ModelProvider provider)
    {
        provider = ModelProvider.OpenAi;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return value.Trim().ToLowerInvariant() switch
        {
            "openai" => true,
            "anthropic" => (provider = ModelProvider.Anthropic) == ModelProvider.Anthropic,
            "gemini" => (provider = ModelProvider.Gemini) == ModelProvider.Gemini,
            _ => false
        };
    }
}

public static class ModelClientFactory
{
    public static IModelClient Create(ModelConfiguration configuration, HttpClient httpClient) => configuration.Provider switch
    {
        ModelProvider.Anthropic => new AnthropicModelClient(configuration, httpClient),
        ModelProvider.Gemini => new GeminiModelClient(configuration, httpClient),
        _ => new OpenAiModelClient(configuration, httpClient)
    };
}

file sealed class OpenAiModelClient(ModelConfiguration config, HttpClient httpClient) : IModelClient
{
    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var response = await GenerateAsync(new ModelRequest { Prompt = prompt }, cancellationToken);
        return response.Text;
    }

    public async Task<ModelResponse> GenerateAsync(ModelRequest modelRequest, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(modelRequest.SystemMessage))
            messages.Add(new { role = "system", content = modelRequest.SystemMessage });
        messages.Add(new { role = "user", content = modelRequest.Prompt });

        var body = new Dictionary<string, object>
        {
            ["model"] = modelRequest.ModelHint ?? config.Model,
            ["messages"] = messages
        };
        if (modelRequest.MaxTokens.HasValue)
            body["max_completion_tokens"] = modelRequest.MaxTokens.Value;

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await IntegrationDefaults.EnsureSuccessAsync("OpenAI", response, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(payload);
        var text = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
            ?? "OpenAI API returned a response with missing or empty content field.";
        return new ModelResponse { Text = text };
    }
}

file sealed class AnthropicModelClient(ModelConfiguration config, HttpClient httpClient) : IModelClient
{
    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var response = await GenerateAsync(new ModelRequest { Prompt = prompt }, cancellationToken);
        return response.Text;
    }

    public async Task<ModelResponse> GenerateAsync(ModelRequest modelRequest, CancellationToken cancellationToken)
    {
        var maxTokens = modelRequest.MaxTokens
            ?? (int.TryParse(Environment.GetEnvironmentVariable("COMPASS_MODEL_MAX_TOKENS"), out var configuredMaxTokens)
                ? configuredMaxTokens
                : IntegrationDefaults.DefaultModelMaxTokens);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", config.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var body = new Dictionary<string, object>
        {
            ["model"] = modelRequest.ModelHint ?? config.Model,
            ["max_tokens"] = maxTokens,
            ["messages"] = new[] { new { role = "user", content = modelRequest.Prompt } }
        };
        if (!string.IsNullOrWhiteSpace(modelRequest.SystemMessage))
            body["system"] = modelRequest.SystemMessage;
        if (modelRequest.Temperature.HasValue)
            body["temperature"] = modelRequest.Temperature.Value;

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await IntegrationDefaults.EnsureSuccessAsync("Anthropic", response, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(payload);
        var text = json.RootElement.GetProperty("content")[0].GetProperty("text").GetString()
            ?? "Anthropic API returned a response with missing or empty text field.";
        return new ModelResponse { Text = text };
    }
}

file sealed class GeminiModelClient(ModelConfiguration config, HttpClient httpClient) : IModelClient
{
    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var response = await GenerateAsync(new ModelRequest { Prompt = prompt }, cancellationToken);
        return response.Text;
    }

    public async Task<ModelResponse> GenerateAsync(ModelRequest modelRequest, CancellationToken cancellationToken)
    {
        var model = modelRequest.ModelHint ?? config.Model;
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", config.ApiKey);

        var parts = new List<object>();
        parts.Add(new { text = modelRequest.Prompt });

        var body = new Dictionary<string, object>
        {
            ["contents"] = new[] { new { parts } }
        };
        if (!string.IsNullOrWhiteSpace(modelRequest.SystemMessage))
            body["systemInstruction"] = new { parts = new[] { new { text = modelRequest.SystemMessage } } };
        if (modelRequest.Temperature.HasValue || modelRequest.MaxTokens.HasValue)
        {
            var generationConfig = new Dictionary<string, object>();
            if (modelRequest.Temperature.HasValue)
                generationConfig["temperature"] = modelRequest.Temperature.Value;
            if (modelRequest.MaxTokens.HasValue)
                generationConfig["maxOutputTokens"] = modelRequest.MaxTokens.Value;
            body["generationConfig"] = generationConfig;
        }

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await IntegrationDefaults.EnsureSuccessAsync("Gemini", response, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(payload);
        var text = json.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()
            ?? "Gemini API returned a response with missing or empty text field.";
        return new ModelResponse { Text = text };
    }
}

public sealed class DiscordChannelBridge(HttpClient httpClient, string botToken, string channelId)
{
    private readonly int _pollIntervalSeconds = int.TryParse(Environment.GetEnvironmentVariable("DISCORD_POLL_INTERVAL_SECONDS"), out var pollIntervalSeconds) && pollIntervalSeconds > 0
        ? pollIntervalSeconds
        : IntegrationDefaults.DefaultDiscordPollIntervalSeconds;
    private readonly int _messageLimit = int.TryParse(Environment.GetEnvironmentVariable("DISCORD_MESSAGE_LIMIT"), out var messageLimit) && messageLimit is > 0 and <= IntegrationDefaults.MaxDiscordMessageLimit
        ? messageLimit
        : IntegrationDefaults.DefaultDiscordMessageLimit;

    public async Task RunAsync(Func<string, CancellationToken, Task<string>> onUserMessage, CancellationToken cancellationToken)
    {
        try
        {
            var lastSeen = await GetNewestMessageIdAsync(cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                var messages = await GetMessagesAsync(cancellationToken);
                foreach (var message in messages.OrderBy(m => m.Snowflake))
                {
                    if (lastSeen is not null && message.Snowflake <= lastSeen.Value)
                        continue;
                    if (message.IsBot || string.IsNullOrWhiteSpace(message.Content))
                        continue;

                    var reply = await onUserMessage(message.Content, cancellationToken);
                    await PostMessageAsync(reply, cancellationToken);
                    lastSeen = message.Snowflake;
                }

                await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSeconds), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task<ulong?> GetNewestMessageIdAsync(CancellationToken cancellationToken)
    {
        var messages = await GetMessagesAsync(cancellationToken);
        return messages.OrderByDescending(m => m.Snowflake).FirstOrDefault()?.Snowflake;
    }

    private async Task<IReadOnlyList<DiscordMessage>> GetMessagesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v10/channels/{channelId}/messages?limit={_messageLimit}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(payload);
        var output = new List<DiscordMessage>();
        foreach (var item in json.RootElement.EnumerateArray())
        {
            var author = item.GetProperty("author");
            var idValue = item.GetProperty("id").GetString() ?? string.Empty;
            if (!ulong.TryParse(idValue, out var snowflake))
                continue;

            output.Add(new DiscordMessage(
                snowflake,
                item.GetProperty("content").GetString() ?? string.Empty,
                author.TryGetProperty("bot", out var botNode) && botNode.GetBoolean()));
        }

        return output;
    }

    private async Task PostMessageAsync(string message, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://discord.com/api/v10/channels/{channelId}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        request.Content = new StringContent(JsonSerializer.Serialize(new { content = message }), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed record DiscordMessage(ulong Snowflake, string Content, bool IsBot);
}
