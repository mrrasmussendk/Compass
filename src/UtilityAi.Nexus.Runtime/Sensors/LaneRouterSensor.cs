using UtilityAi.Nexus.Abstractions;
using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Sensor;

namespace UtilityAi.Nexus.Runtime.Sensors;

public sealed class LaneRouterSensor : ISensor
{
    public Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        if (rt.Bus.TryGet<LaneSelected>(out _)) return Task.CompletedTask;

        var goal = rt.Bus.GetOrDefault<GoalSelected>();
        var lane = goal?.Goal switch
        {
            GoalTag.Stop => Lane.Safety,
            GoalTag.Approve => Lane.Execute,
            GoalTag.Execute => Lane.Execute,
            GoalTag.Clarify => Lane.Interpret,
            GoalTag.Summarize => Lane.Communicate,
            GoalTag.Answer => Lane.Communicate,
            _ => Lane.Interpret,
        };

        rt.Bus.Publish(new LaneSelected(lane));
        return Task.CompletedTask;
    }
}
