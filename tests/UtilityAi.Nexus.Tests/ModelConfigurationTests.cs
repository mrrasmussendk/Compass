using Nexus.SampleHost;

namespace UtilityAi.Nexus.Tests;

public class ModelConfigurationTests
{
    [Theory]
    [InlineData(null, ModelProvider.OpenAi)]
    [InlineData("openai", ModelProvider.OpenAi)]
    [InlineData("anthropic", ModelProvider.Anthropic)]
    [InlineData("gemini", ModelProvider.Gemini)]
    public void TryParseProvider_ParsesKnownValues(string? value, ModelProvider expected)
    {
        var ok = ModelConfiguration.TryParseProvider(value, out var parsed);
        Assert.True(ok);
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void TryParseProvider_ReturnsFalse_ForUnknownProvider()
    {
        var ok = ModelConfiguration.TryParseProvider("unknown-provider", out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryCreateFromEnvironment_UsesProviderSpecificApiKey()
    {
        var priorProvider = Environment.GetEnvironmentVariable("NEXUS_MODEL_PROVIDER");
        var priorOpenAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var priorModel = Environment.GetEnvironmentVariable("NEXUS_MODEL_NAME");
        try
        {
            Environment.SetEnvironmentVariable("NEXUS_MODEL_PROVIDER", "openai");
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
            Environment.SetEnvironmentVariable("NEXUS_MODEL_NAME", "custom-model");

            var ok = ModelConfiguration.TryCreateFromEnvironment(out var config);

            Assert.True(ok);
            Assert.NotNull(config);
            Assert.Equal(ModelProvider.OpenAi, config!.Provider);
            Assert.Equal("test-key", config.ApiKey);
            Assert.Equal("custom-model", config.Model);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NEXUS_MODEL_PROVIDER", priorProvider);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", priorOpenAiKey);
            Environment.SetEnvironmentVariable("NEXUS_MODEL_NAME", priorModel);
        }
    }
}
