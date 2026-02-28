using UtilityAi.Compass.Abstractions;
using UtilityAi.Utils;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Runtime.Sensors;

namespace UtilityAi.Compass.Tests;

public class LaneRouterSensorTests
{
    [Theory]
    [InlineData(GoalTag.Stop, 0.9, Lane.Safety)]
    [InlineData(GoalTag.Answer, 0.9, Lane.Communicate)]
    [InlineData(GoalTag.Execute, 0.9, Lane.Execute)]
    [InlineData(GoalTag.Clarify, 0.9, Lane.Interpret)]
    [InlineData(GoalTag.Execute, 0.5, Lane.Interpret)]
    [InlineData(GoalTag.Approve, 0.5, Lane.Interpret)]
    public async Task SenseAsync_MapsGoalToLane(GoalTag goal, double confidence, Lane expectedLane)
    {
        var sensor = new LaneRouterSensor();
        var bus = new EventBus();
        bus.Publish(new GoalSelected(goal, confidence));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var lane = bus.GetOrDefault<LaneSelected>();
        Assert.NotNull(lane);
        Assert.Equal(expectedLane, lane.Lane);
    }
}
