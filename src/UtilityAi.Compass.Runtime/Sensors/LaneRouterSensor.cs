using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.Runtime.Sensors;

/// <summary>
/// Maps the current <see cref="GoalSelected"/> fact to an execution <see cref="Lane"/>
/// and publishes a <see cref="LaneSelected"/> fact to the EventBus.
/// </summary>
public sealed class LaneRouterSensor : ISensor
{
    /// <summary>
    /// Minimum confidence required before routing potentially side-effectful intent directly to Execute.
    /// </summary>
    private const double ActionConfidenceThreshold = 0.75;

    /// <inheritdoc />
    public Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
    {
        if (rt.Bus.TryGet<LaneSelected>(out _)) return Task.CompletedTask;

        var goal = rt.Bus.GetOrDefault<GoalSelected>();
        if (goal is { Goal: GoalTag.Execute or GoalTag.Approve, Confidence: < ActionConfidenceThreshold })
        {
            rt.Bus.Publish(new LaneSelected(Lane.Interpret));
            return Task.CompletedTask;
        }

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
