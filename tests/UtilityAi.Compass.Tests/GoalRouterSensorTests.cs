using UtilityAi.Compass.Abstractions;
using UtilityAi.Utils;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Runtime.Sensors;

namespace UtilityAi.Compass.Tests;

public class GoalRouterSensorTests
{
    /// <summary>Test double for deterministic model responses.</summary>
    private sealed class StubModelClient(string response) : IModelClient
    {
        public ModelRequest? LastRequest { get; private set; }

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(response);

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ModelResponse { Text = response });
        }
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

    [Fact]
    public async Task SenseAsync_BuildsModelRequestWithWorkflowContext()
    {
        var model = new StubModelClient("{\"goal\":\"Answer\",\"confidence\":0.6}");
        var sensor = new GoalRouterSensor(model);
        var bus = new EventBus();
        bus.Publish(new UserRequest("what now"));
        bus.Publish(new ActiveWorkflow("summarization", "run-1", null, WorkflowStatus.Active));
        bus.Publish(new StepResult(
            StepOutcome.Succeeded,
            "Summary generated",
            new Dictionary<string, object> { ["summary"] = "text", ["tokenCount"] = 123 }));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        Assert.NotNull(model.LastRequest);
        Assert.Equal(64, model.LastRequest.MaxTokens);
        foreach (var goal in Enum.GetNames<GoalTag>())
            Assert.Contains(goal, model.LastRequest.SystemMessage);
        Assert.Contains("wf:summarization (Active)", model.LastRequest.Prompt);
        Assert.Contains("step:Succeeded: Summary generated", model.LastRequest.Prompt);
        Assert.Contains("vars:summary|tokenCount", model.LastRequest.Prompt);
    }

    [Fact]
    public async Task SenseAsync_PublishesMultiStepRequest_FromModelIntent()
    {
        var sensor = new GoalRouterSensor(new StubModelClient(
            "{\"goal\":\"Execute\",\"confidence\":0.92,\"isCompound\":true,\"estimatedSteps\":3}"));
        var bus = new EventBus();
        bus.Publish(new UserRequest("help me handle my onboarding tasks"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var multiStep = bus.GetOrDefault<MultiStepRequest>();
        Assert.NotNull(multiStep);
        Assert.True(multiStep.IsCompound);
        Assert.Equal(3, multiStep.EstimatedSteps);
    }

    [Fact]
    public async Task SenseAsync_DoesNotPublishMultiStepRequest_WhenModelMarksRequestAsNonCompound()
    {
        var sensor = new GoalRouterSensor(new StubModelClient(
            "{\"goal\":\"Answer\",\"confidence\":0.9,\"isCompound\":false,\"estimatedSteps\":1}"));
        var bus = new EventBus();
        bus.Publish(new UserRequest("Ich habe einen Vogel gesehen und dann habe ich ihn erschossen, was soll ich tun?"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        Assert.Null(bus.GetOrDefault<MultiStepRequest>());
    }
}
