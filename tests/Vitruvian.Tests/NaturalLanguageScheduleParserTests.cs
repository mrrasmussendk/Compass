using VitruvianRuntime.Scheduling;
using Xunit;

namespace VitruvianTests;

public sealed class NaturalLanguageScheduleParserTests
{
    [Theory]
    [InlineData("every 5 minutes", 300)]
    [InlineData("every 1 hour", 3600)]
    [InlineData("every 30 seconds", 30)]
    [InlineData("every 2 days", 172800)]
    [InlineData("every minute", 60)]
    [InlineData("every hour", 3600)]
    [InlineData("every day", 86400)]
    [InlineData("every second", 1)]
    [InlineData("daily", 86400)]
    [InlineData("hourly", 3600)]
    [InlineData("every half hour", 1800)]
    [InlineData("every half an hour", 1800)]
    public void TryParseLocal_ValidDescriptions_ReturnsExpectedInterval(string description, int expectedSeconds)
    {
        var result = NaturalLanguageScheduleParser.TryParseLocal(description);

        Assert.NotNull(result);
        Assert.Equal(expectedSeconds, result.Value.TotalSeconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("something random")]
    [InlineData("schedule at noon")]
    public void TryParseLocal_InvalidDescriptions_ReturnsNull(string description)
    {
        var result = NaturalLanguageScheduleParser.TryParseLocal(description);

        Assert.Null(result);
    }

    [Fact]
    public async Task ParseAsync_WithLocalMatch_DoesNotCallLlm()
    {
        var parser = new NaturalLanguageScheduleParser(modelClient: null);

        var result = await parser.ParseAsync("every 10 minutes");

        Assert.NotNull(result);
        Assert.Equal(600, result.Value.TotalSeconds);
    }

    [Fact]
    public async Task ParseAsync_NullOrEmpty_ReturnsNull()
    {
        var parser = new NaturalLanguageScheduleParser();

        Assert.Null(await parser.ParseAsync(null!));
        Assert.Null(await parser.ParseAsync(""));
        Assert.Null(await parser.ParseAsync("  "));
    }
}
