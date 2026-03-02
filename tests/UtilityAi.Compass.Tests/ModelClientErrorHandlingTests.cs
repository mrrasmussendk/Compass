using System.Net;
using System.Text.Json;
using UtilityAi.Compass.Cli;
using UtilityAi.Compass.Abstractions.Interfaces;

namespace UtilityAi.Compass.Tests;

public class ModelClientErrorHandlingTests
{
    private const int InvalidModelProvider = 999;

    private sealed class StubHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            };
            return response;
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }

    [Theory]
    [InlineData(ModelProvider.OpenAi)]
    [InlineData(ModelProvider.Anthropic)]
    [InlineData(ModelProvider.Gemini)]
    public async Task GenerateAsync_ThrowsWithApiBody_On400(ModelProvider provider)
    {
        var errorJson = """{"error":{"message":"Invalid model: gpt-5.2"}}""";
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.BadRequest, errorJson));
        var config = new ModelConfiguration(provider, "test-key", "test-model");
        var client = ModelClientFactory.Create(config, httpClient);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateAsync("Hello", CancellationToken.None));

        Assert.Contains("400", ex.Message);
        Assert.Contains("Invalid model: gpt-5.2", ex.Message);
    }

    [Theory]
    [InlineData(ModelProvider.OpenAi, "OpenAI")]
    [InlineData(ModelProvider.Anthropic, "Anthropic")]
    [InlineData(ModelProvider.Gemini, "Gemini")]
    public async Task GenerateAsync_IncludesProviderName_OnError(ModelProvider provider, string expectedName)
    {
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.Unauthorized, "Unauthorized"));
        var config = new ModelConfiguration(provider, "bad-key", "test-model");
        var client = ModelClientFactory.Create(config, httpClient);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateAsync("Hello", CancellationToken.None));

        Assert.Contains(expectedName, ex.Message);
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_Succeeds_WhenApiReturns200()
    {
        var successJson = """{"output_text":"Hi there!"}""";
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.OK, successJson));
        var config = new ModelConfiguration(ModelProvider.OpenAi, "test-key", "test-model");
        var client = ModelClientFactory.Create(config, httpClient);

        var result = await client.GenerateAsync("Hello", CancellationToken.None);

        Assert.Equal("Hi there!", result);
    }

    [Fact]
    public async Task OpenAi_GenerateAsync_ParsesResponsesApiOutputArray()
    {
        var successJson = """{"output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"Hello from Responses API","annotations":[]}]}]}""";
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.OK, successJson));
        var config = new ModelConfiguration(ModelProvider.OpenAi, "test-key", "test-model");
        var client = ModelClientFactory.Create(config, httpClient);

        var result = await client.GenerateAsync("Hello", CancellationToken.None);

        Assert.Equal("Hello from Responses API", result);
    }

    [Fact]
    public async Task OpenAi_GenerateAsync_SendsInputAndInstructions()
    {
        var successJson = """{"output_text":"Hi"}""";
        var handler = new StubHandler(HttpStatusCode.OK, successJson);
        using var httpClient = new HttpClient(handler);
        var config = new ModelConfiguration(ModelProvider.OpenAi, "test-key", "test-model");
        var client = ModelClientFactory.Create(config, httpClient);

        await client.GenerateAsync(new ModelRequest { Prompt = "Hello", SystemMessage = "Be helpful", MaxTokens = 100, Temperature = 0.7 }, CancellationToken.None);

        Assert.NotNull(handler.LastRequestBody);
        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        Assert.True(doc.RootElement.TryGetProperty("input", out var input));
        Assert.Equal("Hello", input.GetString());
        Assert.True(doc.RootElement.TryGetProperty("instructions", out var instructions));
        Assert.Equal("Be helpful", instructions.GetString());
        Assert.False(doc.RootElement.TryGetProperty("messages", out _));
        Assert.False(doc.RootElement.TryGetProperty("max_completion_tokens", out _));
        Assert.False(doc.RootElement.TryGetProperty("max_tokens", out _));
        Assert.False(doc.RootElement.TryGetProperty("temperature", out _));
    }

    [Fact]
    public void ModelClientFactory_Throws_ForUnsupportedProvider()
    {
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.OK, """{"output_text":"ok"}"""));
        var config = new ModelConfiguration((ModelProvider)InvalidModelProvider, "test-key", "test-model");

        var ex = Assert.Throws<NotSupportedException>(() => ModelClientFactory.Create(config, httpClient));

        Assert.Contains("Unsupported model provider", ex.Message);
    }

    [Theory]
    [InlineData(ModelProvider.OpenAi, "OpenAI")]
    [InlineData(ModelProvider.Anthropic, "Anthropic")]
    [InlineData(ModelProvider.Gemini, "Gemini")]
    public async Task GenerateAsync_ThrowsInvalidOperation_OnTaskCanceled(ModelProvider provider, string expectedProvider)
    {
        using var httpClient = new HttpClient(new ThrowingHandler(new TaskCanceledException("network timeout")));
        var config = new ModelConfiguration(provider, "test-key", "test-model");
        var client = ModelClientFactory.Create(config, httpClient);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateAsync("Hello", CancellationToken.None));

        Assert.Contains(expectedProvider, ex.Message);
        Assert.Contains("timed out", ex.Message);
    }
}
