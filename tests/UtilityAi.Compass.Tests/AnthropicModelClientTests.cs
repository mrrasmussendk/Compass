using System.Net;
using System.Text;
using System.Text.Json;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Cli;
using Xunit;

namespace UtilityAi.Compass.Tests;

public sealed class AnthropicModelClientTests
{
    private static IModelClient CreateClient(HttpMessageHandler handler)
    {
        var config = new ModelConfiguration(ModelProvider.Anthropic, "test-key", "claude-3-5-haiku-latest");
        return ModelClientFactory.Create(config, new HttpClient(handler));
    }

    private sealed class FakeHandler(string responseJson) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    [Fact]
    public async Task GenerateAsync_TextResponse_ReturnsText()
    {
        var response = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { type = "text", text = "Hello from Claude" }
            }
        });

        var handler = new FakeHandler(response);
        var client = CreateClient(handler);

        var result = await client.GenerateAsync("Hi", CancellationToken.None);

        Assert.Equal("Hello from Claude", result);
    }

    [Fact]
    public async Task GenerateAsync_WithTools_SendsToolsInRequest()
    {
        var response = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { type = "text", text = "I'll search for that." }
            }
        });

        var handler = new FakeHandler(response);
        var client = CreateClient(handler);

        var tools = new List<ModelTool>
        {
            new("web_search", "Search the web", new Dictionary<string, string>
            {
                ["query"] = "string"
            })
        };

        await client.GenerateAsync(new ModelRequest
        {
            Prompt = "Search for weather",
            Tools = tools
        }, CancellationToken.None);

        Assert.NotNull(handler.LastRequestBody);
        using var json = JsonDocument.Parse(handler.LastRequestBody);
        Assert.True(json.RootElement.TryGetProperty("tools", out var toolsArray));
        Assert.Equal(1, toolsArray.GetArrayLength());

        var tool = toolsArray[0];
        Assert.Equal("web_search", tool.GetProperty("name").GetString());
        Assert.Equal("Search the web", tool.GetProperty("description").GetString());

        var schema = tool.GetProperty("input_schema");
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.True(schema.TryGetProperty("properties", out var props));
        Assert.Equal("string", props.GetProperty("query").GetProperty("type").GetString());
    }

    [Fact]
    public async Task GenerateAsync_ToolUseResponse_ReturnsToolCallAndArguments()
    {
        var response = JsonSerializer.Serialize(new
        {
            content = new object[]
            {
                new { type = "text", text = "Let me search for that." },
                new
                {
                    type = "tool_use",
                    id = "toolu_123",
                    name = "web_search",
                    input = new { query = "weather today" }
                }
            }
        });

        var handler = new FakeHandler(response);
        var client = CreateClient(handler);

        var result = await client.GenerateAsync(new ModelRequest
        {
            Prompt = "What's the weather?",
            Tools = new List<ModelTool>
            {
                new("web_search", "Search the web", new Dictionary<string, string>
                {
                    ["query"] = "string"
                })
            }
        }, CancellationToken.None);

        Assert.Equal("Let me search for that.", result.Text);
        Assert.Equal("web_search", result.ToolCall);
        Assert.NotNull(result.ToolArguments);
        using var args = JsonDocument.Parse(result.ToolArguments);
        Assert.Equal("weather today", args.RootElement.GetProperty("query").GetString());
    }

    [Fact]
    public async Task GenerateAsync_ToolUseOnlyResponse_FallbackText()
    {
        var response = JsonSerializer.Serialize(new
        {
            content = new object[]
            {
                new
                {
                    type = "tool_use",
                    id = "toolu_456",
                    name = "gmail_read_messages",
                    input = new { query = "inbox", maxResults = 5 }
                }
            }
        });

        var handler = new FakeHandler(response);
        var client = CreateClient(handler);

        var result = await client.GenerateAsync(new ModelRequest
        {
            Prompt = "Read my email",
            Tools = new List<ModelTool>
            {
                new("gmail_read_messages", "Read Gmail", new Dictionary<string, string>
                {
                    ["query"] = "string",
                    ["maxResults"] = "number"
                })
            }
        }, CancellationToken.None);

        Assert.Equal("Tool call: gmail_read_messages", result.Text);
        Assert.Equal("gmail_read_messages", result.ToolCall);
        Assert.NotNull(result.ToolArguments);
    }

    [Fact]
    public async Task GenerateAsync_WithoutTools_DoesNotIncludeToolsInRequest()
    {
        var response = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { type = "text", text = "Hello!" }
            }
        });

        var handler = new FakeHandler(response);
        var client = CreateClient(handler);

        await client.GenerateAsync(new ModelRequest { Prompt = "Hello" }, CancellationToken.None);

        Assert.NotNull(handler.LastRequestBody);
        using var json = JsonDocument.Parse(handler.LastRequestBody);
        Assert.False(json.RootElement.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task GenerateAsync_MultipleTools_AllSentInRequest()
    {
        var response = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { type = "text", text = "OK" }
            }
        });

        var handler = new FakeHandler(response);
        var client = CreateClient(handler);

        var tools = new List<ModelTool>
        {
            new("web_search", "Search the web", new Dictionary<string, string> { ["query"] = "string" }),
            new("gmail_read", "Read email", new Dictionary<string, string> { ["query"] = "string", ["maxResults"] = "number" }),
            new("gmail_draft", "Draft email", new Dictionary<string, string> { ["to"] = "string", ["subject"] = "string", ["body"] = "string" })
        };

        await client.GenerateAsync(new ModelRequest
        {
            Prompt = "Help me",
            Tools = tools
        }, CancellationToken.None);

        Assert.NotNull(handler.LastRequestBody);
        using var json = JsonDocument.Parse(handler.LastRequestBody);
        Assert.True(json.RootElement.TryGetProperty("tools", out var toolsArray));
        Assert.Equal(3, toolsArray.GetArrayLength());
    }

    [Fact]
    public async Task CompleteAsync_WithTools_PassesToolsToRequest()
    {
        var response = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { type = "text", text = "Search result here" }
            }
        });

        var handler = new FakeHandler(response);
        var client = CreateClient(handler);

        var tools = new List<ModelTool>
        {
            new("web_search", "Search the web", new Dictionary<string, string> { ["query"] = "string" })
        };

        var result = await client.CompleteAsync("You are helpful", "Search for weather", tools, CancellationToken.None);

        Assert.Equal("Search result here", result);
        Assert.NotNull(handler.LastRequestBody);
        using var json = JsonDocument.Parse(handler.LastRequestBody);
        Assert.True(json.RootElement.TryGetProperty("tools", out _));
        Assert.Equal("You are helpful", json.RootElement.GetProperty("system").GetString());
    }
}
