using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.StandardModules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class WebSearchModuleTests
{
    private sealed class StubModelClient : IModelClient
    {
        private readonly string _response;
        public StubModelClient(string response = "search result") => _response = response;

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ModelResponse { Text = _response });
    }

    [Fact]
    public void Propose_ReturnsProposal_WhenUserRequestExists()
    {
        var module = new WebSearchModule(new StubModelClient());
        var bus = new EventBus();
        bus.Publish(new UserRequest("search for dotnet 10 features"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("web-search.query", proposals[0].Id);
        Assert.Equal("Search the web for an answer", proposals[0].Description);
    }

    [Fact]
    public void Propose_ReturnsEmpty_WhenNoUserRequest()
    {
        var module = new WebSearchModule(new StubModelClient());
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public async Task Propose_ActPublishesSearchResult_WhenExecuted()
    {
        var module = new WebSearchModule(new StubModelClient("Here are the top results"));
        var bus = new EventBus();
        bus.Publish(new UserRequest("search for dotnet 10 features"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);

        await proposals[0].Act(CancellationToken.None);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        Assert.Equal("Here are the top results", response.Text);
    }

    [Fact]
    public void Propose_HasPositiveUtility()
    {
        var module = new WebSearchModule(new StubModelClient());
        var bus = new EventBus();
        bus.Publish(new UserRequest("search for something"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);
        Assert.True(proposals[0].Utility(rt) > 0);
    }
}
