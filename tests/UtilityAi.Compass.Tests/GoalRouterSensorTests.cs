using UtilityAi.Compass.Abstractions;
using UtilityAi.Utils;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Runtime.Sensors;

namespace UtilityAi.Compass.Tests;

public class GoalRouterSensorTests
{
    private sealed class StubModelClient(string response) : IModelClient
    {
        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(response);

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ModelResponse { Text = response });
    }

    [Theory]
    [InlineData("{\"goal\":\"Stop\",\"confidence\":0.95}", GoalTag.Stop)]
    [InlineData("{\"goal\":\"Summarize\",\"confidence\":0.85}", GoalTag.Summarize)]
    [InlineData("{\"goal\":\"Answer\",\"confidence\":0.70}", GoalTag.Answer)]
    public async Task SenseAsync_ClassifiesGoalFromModelResponse(string modelResponse, GoalTag expectedGoal)
    {
        var sensor = new GoalRouterSensor(new StubModelClient(modelResponse));
        var bus = new EventBus();
        bus.Publish(new UserRequest("user prompt"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var goal = bus.GetOrDefault<GoalSelected>();
        Assert.NotNull(goal);
        Assert.Equal(expectedGoal, goal.Goal);
    }

    [Fact]
    public async Task SenseAsync_DefaultsToAnswer_WhenNoModelClientConfigured()
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
    public async Task SenseAsync_DefaultsToAnswer_WhenModelResponseIsInvalidJson()
    {
        var sensor = new GoalRouterSensor(new StubModelClient("not-json"));
        var bus = new EventBus();
        bus.Publish(new UserRequest("please stop"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var goal = bus.GetOrDefault<GoalSelected>();
        Assert.NotNull(goal);
        Assert.Equal(GoalTag.Answer, goal.Goal);
    }

    [Fact]
    public async Task SenseAsync_UsesDefaultConfidence_WhenModelOmitsConfidence()
    {
        var sensor = new GoalRouterSensor(new StubModelClient("{\"goal\":\"Execute\"}"));
        var bus = new EventBus();
        bus.Publish(new UserRequest("run the workflow"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var goal = bus.GetOrDefault<GoalSelected>();
        Assert.NotNull(goal);
        Assert.Equal(GoalTag.Execute, goal.Goal);
        Assert.Equal(0.7, goal.Confidence);
    }
}
