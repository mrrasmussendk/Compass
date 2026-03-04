using VitruvianCli;
using Xunit;

namespace VitruvianTests;

public sealed class CliHostedServiceTests
{
    [Theory]
    [InlineData("/schedule \"every 5 minutes\" check weather", "every 5 minutes", "check weather")]
    [InlineData("/schedule \"daily\" send summary email", "daily", "send summary email")]
    [InlineData("/schedule \"every 30 seconds\" ping server", "every 30 seconds", "ping server")]
    public void TryParseScheduleCommand_ValidInput_ReturnsTrue(string input, string expectedSchedule, string expectedTask)
    {
        var result = CliHostedService.TryParseScheduleCommand(input, out var schedule, out var task);

        Assert.True(result);
        Assert.Equal(expectedSchedule, schedule);
        Assert.Equal(expectedTask, task);
    }

    [Theory]
    [InlineData("/help")]
    [InlineData("/schedule")]
    [InlineData("/schedule \"\"")]
    [InlineData("/schedule \"every 5 minutes\"")]  // no task after schedule
    [InlineData("schedule \"every 5 minutes\" test")]  // missing /
    public void TryParseScheduleCommand_InvalidInput_ReturnsFalse(string input)
    {
        var result = CliHostedService.TryParseScheduleCommand(input, out _, out _);

        Assert.False(result);
    }
}
