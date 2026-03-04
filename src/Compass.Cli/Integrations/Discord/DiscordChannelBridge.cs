using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VitruvianCli;

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
