using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace VitruvianCli;

public sealed record WebSocketInboundMessage(string Request, string? Domain = null, string? UserId = null);

public sealed class WebSocketChannelBridge(string listenUrl, string? publicUrl = null, string? defaultDomain = null)
{
    private readonly string _listenerPrefix = NormalizeListenerPrefix(listenUrl);
    private readonly string _publicUrl = NormalizePublicUrl(publicUrl, listenUrl);

    public static string NormalizeListenerPrefix(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("WebSocket URL must be provided.", nameof(url));

        var normalized = url.Trim();
        if (normalized.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
            normalized = $"http://{normalized[5..]}";
        else if (normalized.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            normalized = $"https://{normalized[6..]}";

        if (!normalized.EndsWith('/'))
            normalized += "/";
        return normalized;
    }

    public static string NormalizePublicUrl(string? publicUrl, string listenUrl)
    {
        if (!string.IsNullOrWhiteSpace(publicUrl))
            return publicUrl.Trim();

        if (listenUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return $"ws://{listenUrl[7..].TrimEnd('/')}/";
        if (listenUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return $"wss://{listenUrl[8..].TrimEnd('/')}/";

        return listenUrl.TrimEnd('/') + "/";
    }

    public static WebSocketInboundMessage ParseInbound(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return new WebSocketInboundMessage(string.Empty);

        var trimmed = payload.Trim();
        if (!trimmed.StartsWith('{'))
            return new WebSocketInboundMessage(trimmed);

        try
        {
            using var json = JsonDocument.Parse(trimmed);
            var root = json.RootElement;
            var request = root.TryGetProperty("request", out var requestNode)
                ? requestNode.GetString()
                : root.TryGetProperty("message", out var messageNode)
                    ? messageNode.GetString()
                    : trimmed;
            var domain = root.TryGetProperty("domain", out var domainNode) ? domainNode.GetString() : null;
            var userId = root.TryGetProperty("userId", out var userNode) ? userNode.GetString() : null;
            return new WebSocketInboundMessage(request ?? string.Empty, domain, userId);
        }
        catch (JsonException)
        {
            return new WebSocketInboundMessage(trimmed);
        }
    }

    public static string ToProcessorInput(WebSocketInboundMessage inbound, string? fallbackDomain)
    {
        var domain = string.IsNullOrWhiteSpace(inbound.Domain) ? fallbackDomain : inbound.Domain;
        if (string.IsNullOrWhiteSpace(domain))
            return inbound.Request;

        return $"[domain:{domain}] {inbound.Request}";
    }

    public static string BuildOutboundPayload(string response, WebSocketInboundMessage inbound, string? fallbackDomain)
    {
        var effectiveDomain = string.IsNullOrWhiteSpace(inbound.Domain) ? fallbackDomain : inbound.Domain;
        return JsonSerializer.Serialize(new
        {
            response,
            domain = effectiveDomain,
            userId = inbound.UserId,
            helper = "Send JSON payloads with {\"request\":\"...\",\"domain\":\"your-domain\",\"userId\":\"optional-user\"} for better deploy-time routing."
        });
    }

    public async Task RunAsync(Func<string, CancellationToken, Task<string>> onUserMessage, CancellationToken cancellationToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(_listenerPrefix);
        listener.Start();
        Console.WriteLine("Vitruvian CLI started in WebSocket mode.");
        Console.WriteLine($"WebSocket listen prefix: {_listenerPrefix}");
        Console.WriteLine($"WebSocket connect URL: {_publicUrl}");
        Console.WriteLine("Developer helpers: include `domain` and `userId` in payload JSON for better routing and observability.");

        using var registration = cancellationToken.Register(() =>
        {
            if (listener.IsListening)
                listener.Stop();
        });

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext? context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (context is null)
                    continue;

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    var error = Encoding.UTF8.GetBytes("Expected a WebSocket upgrade request.");
                    await context.Response.OutputStream.WriteAsync(error, cancellationToken);
                    context.Response.Close();
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                await HandleSocketAsync(wsContext.WebSocket, onUserMessage, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private async Task HandleSocketAsync(WebSocket socket, Func<string, CancellationToken, Task<string>> onUserMessage, CancellationToken cancellationToken)
    {
        var buffer = new byte[4 * 1024];
        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var payloadStream = new MemoryStream();
                WebSocketReceiveResult receiveResult;
                do
                {
                    receiveResult = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", cancellationToken);
                        return;
                    }

                    payloadStream.Write(buffer, 0, receiveResult.Count);
                } while (!receiveResult.EndOfMessage);

                var payload = Encoding.UTF8.GetString(payloadStream.ToArray());
                var inbound = ParseInbound(payload);
                var request = ToProcessorInput(inbound, defaultDomain);
                var response = await onUserMessage(request, cancellationToken);
                var outbound = BuildOutboundPayload(response, inbound, defaultDomain);
                var responseBytes = Encoding.UTF8.GetBytes(outbound);
                await socket.SendAsync(responseBytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
}
