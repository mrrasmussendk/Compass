using UtilityAi.Nexus.Abstractions;

namespace UtilityAi.Nexus.Tests;

public class CommandLineCommonActionsTests
{
    [Theory]
    [InlineData("read file", true)]
    [InlineData("write file", true)]
    [InlineData("update file", true)]
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
}
