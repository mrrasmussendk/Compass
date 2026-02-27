using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.StandardModules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class WebSearchWorkflowTests
{
    private sealed class StubModelClient : IModelClient
    {
        private readonly string _response;
        public ModelRequest? LastRequest { get; private set; }
        public StubModelClient(string response = "search result") => _response = response;

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ModelResponse { Text = _response });
        }
    }

    [Fact]
    public void Define_ReturnsCorrectWorkflowDefinition()
    {
        var wf = new WebSearchWorkflow(new StubModelClient());
        var def = wf.Define();

        Assert.Equal("web-search", def.WorkflowId);
        Assert.Equal("Web Search", def.DisplayName);
        Assert.Contains(GoalTag.Answer, def.Goals);
        Assert.Contains(Lane.Execute, def.Lanes);
        Assert.Single(def.Steps);
        Assert.Equal("query", def.Steps[0].StepId);
        Assert.True(def.CanInterrupt);
        Assert.Equal(0.4, def.EstimatedCost);
        Assert.Equal(0.1, def.RiskLevel);
    }

    [Fact]
    public void ProposeStart_ReturnsProposal_WhenUserRequestExists()
    {
        var wf = new WebSearchWorkflow(new StubModelClient());
        var bus = new EventBus();
        bus.Publish(new UserRequest("search for dotnet 10 features"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = wf.ProposeStart(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("web-search.query", proposals[0].Id);
    }

    [Fact]
    public void ProposeStart_ReturnsEmpty_WhenNoUserRequest()
    {
        var wf = new WebSearchWorkflow(new StubModelClient());
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        Assert.Empty(wf.ProposeStart(rt));
    }

    [Fact]
    public async Task ProposeStart_ActPublishesResult_WhenExecuted()
    {
        var stub = new StubModelClient("Here are results");
        var wf = new WebSearchWorkflow(stub);
        var bus = new EventBus();
        bus.Publish(new UserRequest("search for dotnet features"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = wf.ProposeStart(rt).ToList();
        await proposals[0].Act(CancellationToken.None);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        Assert.Equal("Here are results", response.Text);

        Assert.NotNull(stub.LastRequest);
        Assert.NotNull(stub.LastRequest!.Tools);
        Assert.Single(stub.LastRequest.Tools);
        Assert.Equal("web_search", stub.LastRequest.Tools[0].Name);
    }
}
