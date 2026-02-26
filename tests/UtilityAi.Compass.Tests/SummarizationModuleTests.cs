using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.StandardModules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class SummarizationModuleTests
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
    public void Propose_ReturnsProposal_WhenUserRequestExists()
    {
        var module = new SummarizationModule(new StubModelClient());
        var bus = new EventBus();
        bus.Publish(new UserRequest("summarize this long text about AI"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("summarization.summarize", proposals[0].Id);
        Assert.Equal("Summarize the provided content", proposals[0].Description);
    }

    [Fact]
    public void Propose_ReturnsEmpty_WhenNoUserRequest()
    {
        var module = new SummarizationModule(new StubModelClient());
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public async Task Propose_ActPublishesSummary_WhenExecuted()
    {
        var module = new SummarizationModule(new StubModelClient("This is a concise summary."));
        var bus = new EventBus();
        bus.Publish(new UserRequest("summarize this article about climate change"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);

        await proposals[0].Act(CancellationToken.None);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        Assert.Equal("This is a concise summary.", response.Text);
    }

    [Fact]
    public void Propose_HasPositiveUtility()
    {
        var module = new SummarizationModule(new StubModelClient());
        var bus = new EventBus();
        bus.Publish(new UserRequest("summarize this"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);
        Assert.True(proposals[0].Utility(rt) > 0);
    }

    [Fact]
    public void BuildStructuredRequest_ProducesValidJson()
    {
        var json = SummarizationModule.BuildStructuredRequest("gpt-5.2", "Summarize this article about AI.");

        Assert.NotNull(json);
        Assert.Contains("gpt-5.2", json);
        Assert.Contains("summary_result", json);
        Assert.Contains("Summarize this article about AI.", json);
    }
}
