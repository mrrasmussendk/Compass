using System.Net;
using Compass.SampleHost;
using UtilityAi.Compass.Abstractions.Interfaces;

namespace UtilityAi.Compass.Tests;

public class ModelClientErrorHandlingTests
{
    private sealed class StubHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            };
            return Task.FromResult(response);
        }
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

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
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

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GenerateAsync("Hello", CancellationToken.None));

        Assert.Contains(expectedName, ex.Message);
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_Succeeds_WhenApiReturns200()
    {
        var successJson = """{"choices":[{"message":{"content":"Hi there!"}}]}""";
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.OK, successJson));
        var config = new ModelConfiguration(ModelProvider.OpenAi, "test-key", "test-model");
        var client = ModelClientFactory.Create(config, httpClient);

        var result = await client.GenerateAsync("Hello", CancellationToken.None);

        Assert.Equal("Hi there!", result);
    }
}
