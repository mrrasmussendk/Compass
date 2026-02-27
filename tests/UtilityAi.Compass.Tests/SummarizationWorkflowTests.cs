using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.StandardModules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class SummarizationWorkflowTests
{
    private sealed class StubModelClient : IModelClient
    {
        private readonly string _response;
        public StubModelClient(string response = "summary text") => _response = response;

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ModelResponse { Text = _response });
    }

    [Fact]
    public void Define_ReturnsCorrectWorkflowDefinition()
    {
        var wf = new SummarizationWorkflow(new StubModelClient());
        var def = wf.Define();

        Assert.Equal("summarization", def.WorkflowId);
        Assert.Equal("Summarize Content", def.DisplayName);
        Assert.Contains(GoalTag.Summarize, def.Goals);
        Assert.Contains(Lane.Communicate, def.Lanes);
        Assert.Single(def.Steps);
        Assert.Equal("summarize", def.Steps[0].StepId);
        Assert.True(def.CanInterrupt);
        Assert.Equal(0.3, def.EstimatedCost);
    }

    [Fact]
    public void ProposeStart_ReturnsProposal_WhenUserRequestExists()
    {
        var wf = new SummarizationWorkflow(new StubModelClient());
        var bus = new EventBus();
        bus.Publish(new UserRequest("summarize this article"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = wf.ProposeStart(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("summarization.summarize", proposals[0].Id);
    }

    [Fact]
    public void ProposeStart_ReturnsEmpty_WhenNoUserRequest()
    {
        var wf = new SummarizationWorkflow(new StubModelClient());
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        Assert.Empty(wf.ProposeStart(rt));
    }

    [Fact]
    public async Task ProposeStart_ActPublishesSummary_WhenExecuted()
    {
        var wf = new SummarizationWorkflow(new StubModelClient("This is a summary."));
        var bus = new EventBus();
        bus.Publish(new UserRequest("summarize this article"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = wf.ProposeStart(rt).ToList();
        await proposals[0].Act(CancellationToken.None);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        Assert.Equal("This is a summary.", response.Text);

        var stepResult = bus.GetOrDefault<StepResult>();
        Assert.NotNull(stepResult);
        Assert.Equal(StepOutcome.Succeeded, stepResult.Outcome);
    }
}
