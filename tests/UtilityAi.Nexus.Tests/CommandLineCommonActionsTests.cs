using UtilityAi.Nexus.Abstractions;

namespace UtilityAi.Nexus.Tests;

public class CommandLineCommonActionsTests
{
    [Theory]
    [InlineData("read file", true)]
    [InlineData("write file", true)]
    [InlineData("update file", true)]
    [InlineData("reading file", false)]
    [InlineData("updated file", false)]
    [InlineData("created file", false)]
    [InlineData("summarize this", false)]
    public void IsCommonAction_DetectsReadWriteUpdate(string text, bool expected)
    {
        var result = CommandLineCommonActions.IsCommonAction(text);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Score_ReturnsHitScore_WhenKeywordMatches()
    {
        var score = CommandLineCommonActions.Score("please read config", CommandLineCommonActions.ReadKeywords);
        Assert.Equal(0.85, score);
    }

    [Theory]
    [InlineData("something unrelated")]
    [InlineData("")]
    [InlineData(null)]
    public void Score_ReturnsZero_WhenNoKeywordOrNoInput(string? text)
    {
        var score = CommandLineCommonActions.Score(text, CommandLineCommonActions.WriteKeywords);
        Assert.Equal(0.0, score);
    }
}
