using UtilityAi.Compass.Abstractions;
using UtilityAi.Utils;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Runtime.Sensors;

namespace UtilityAi.Compass.Tests;

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

    [Fact]
    public async Task SenseAsync_DoesNotMatchPartialWord_Stop()
    {
        var sensor = new GoalRouterSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest("the process has stopped already"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var goal = bus.GetOrDefault<GoalSelected>();
        Assert.NotNull(goal);
        Assert.Equal(GoalTag.Answer, goal.Goal);
    }

    [Fact]
    public async Task SenseAsync_DoesNotMatchPartialWord_Execute()
    {
        var sensor = new GoalRouterSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest("undo that last step"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var goal = bus.GetOrDefault<GoalSelected>();
        Assert.NotNull(goal);
        Assert.Equal(GoalTag.Answer, goal.Goal);
    }

    [Fact]
    public async Task SenseAsync_PrefersHigherConfidenceMatch()
    {
        var sensor = new GoalRouterSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest("please summarize and then stop"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var goal = bus.GetOrDefault<GoalSelected>();
        Assert.NotNull(goal);
        Assert.Equal(GoalTag.Stop, goal.Goal);
        Assert.Equal(0.95, goal.Confidence);
    }
}
