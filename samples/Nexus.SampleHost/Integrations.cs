using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Nexus.SampleHost;

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
        var providerText = Environment.GetEnvironmentVariable("NEXUS_MODEL_PROVIDER");
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

        var model = Environment.GetEnvironmentVariable("NEXUS_MODEL_NAME");
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

public interface IModelClient
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken);
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
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = config.Model,
            messages = new[] { new { role = "user", content = prompt } }
        }), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(payload);
        return json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
            ?? "OpenAI returned an empty response.";
    }
}

file sealed class AnthropicModelClient(ModelConfiguration config, HttpClient httpClient) : IModelClient
{
    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", config.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = config.Model,
            max_tokens = 512,
            messages = new[] { new { role = "user", content = prompt } }
        }), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(payload);
        return json.RootElement.GetProperty("content")[0].GetProperty("text").GetString()
            ?? "Anthropic returned an empty response.";
    }
}

file sealed class GeminiModelClient(ModelConfiguration config, HttpClient httpClient) : IModelClient
{
    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{config.Model}:generateContent?key={Uri.EscapeDataString(config.ApiKey)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        }), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(payload);
        return json.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()
            ?? "Gemini returned an empty response.";
    }
}

public sealed class DiscordChannelBridge(HttpClient httpClient, string botToken, string channelId)
{
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

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
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
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v10/channels/{channelId}/messages?limit=25");
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
