using UtilityAi.Compass.Abstractions;
using UtilityAi.Utils;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Runtime.Sensors;

namespace UtilityAi.Compass.Tests;

public class LaneRouterSensorTests
{
    [Theory]
    [InlineData(GoalTag.Stop, Lane.Safety)]
    [InlineData(GoalTag.Answer, Lane.Communicate)]
    [InlineData(GoalTag.Execute, Lane.Execute)]
    [InlineData(GoalTag.Clarify, Lane.Interpret)]
    public async Task SenseAsync_MapsGoalToLane(GoalTag goal, Lane expectedLane)
    {
        var sensor = new LaneRouterSensor();
        var bus = new EventBus();
        bus.Publish(new GoalSelected(goal, 0.9));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var lane = bus.GetOrDefault<LaneSelected>();
        Assert.NotNull(lane);
        Assert.Equal(expectedLane, lane.Lane);
    }
}
