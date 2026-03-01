using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.StandardModules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class GmailModuleTests
{
    private sealed class StubModelClient : IModelClient
    {
        private readonly string _response;
        public ModelRequest? LastRequest { get; private set; }

        public StubModelClient(string response = "gmail result") => _response = response;

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ModelResponse { Text = _response });
        }
    }

    [Fact]
    public void Propose_ReturnsProposal_ForGmailRequest()
    {
        var module = new GmailModule(new StubModelClient());
        var bus = new EventBus();
        bus.Publish(new UserRequest("read my gmail inbox"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("gmail.read-draft", proposals[0].Id);
    }

    [Fact]
    public void Propose_ReturnsEmpty_ForNonGmailRequest()
    {
        var module = new GmailModule(new StubModelClient());
        var bus = new EventBus();
        bus.Publish(new UserRequest("summarize this document"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public async Task Propose_UsesReadAndDraftTools_WhenExecuted()
    {
        var stub = new StubModelClient("draft prepared");
        var module = new GmailModule(stub);
        var bus = new EventBus();
        bus.Publish(new UserRequest("draft a reply to latest gmail message"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);

        await proposals[0].Act(CancellationToken.None);

        Assert.NotNull(stub.LastRequest);
        Assert.NotNull(stub.LastRequest!.Tools);
        Assert.Equal(2, stub.LastRequest.Tools.Count);
        Assert.Contains(stub.LastRequest.Tools, t => t.Name == "gmail_read_messages");
        Assert.Contains(stub.LastRequest.Tools, t => t.Name == "gmail_create_draft");
        Assert.Contains("https://www.googleapis.com/auth/gmail.compose", GmailModule.RequiredGoogleScopes);
        Assert.Equal("draft prepared", bus.GetOrDefault<AiResponse>()?.Text);
    }
}
