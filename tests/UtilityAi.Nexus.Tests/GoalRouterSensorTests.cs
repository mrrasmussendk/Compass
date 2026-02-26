using UtilityAi.Nexus.Abstractions;
using UtilityAi.Utils;
using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Nexus.Runtime.Sensors;

namespace UtilityAi.Nexus.Tests;

public class GoalRouterSensorTests
{
    [Theory]
    [InlineData("please stop", GoalTag.Stop)]
    [InlineData("summarize this", GoalTag.Summarize)]
    [InlineData("what is this?", GoalTag.Answer)]
    public async Task SenseAsync_ClassifiesGoalCorrectly(string text, GoalTag expectedGoal)
    {
        var sensor = new GoalRouterSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest(text));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var goal = bus.GetOrDefault<GoalSelected>();
        Assert.NotNull(goal);
        Assert.Equal(expectedGoal, goal.Goal);
    }

    [Fact]
    public async Task SenseAsync_DefaultsToAnswer_WhenNoKeywordMatches()
    {
        var sensor = new GoalRouterSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest("some random text"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var goal = bus.GetOrDefault<GoalSelected>();
        Assert.NotNull(goal);
        Assert.Equal(GoalTag.Answer, goal.Goal);
    }
}
