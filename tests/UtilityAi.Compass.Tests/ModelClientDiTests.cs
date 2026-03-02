using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.StandardModules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class ModelClientDiTests
{
    private sealed class StubModelClient : IModelClient
    {
        public string LastPrompt { get; private set; } = "";
        public ModelRequest? LastModelRequest { get; private set; }

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            return Task.FromResult($"stub:{prompt}");
        }

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            LastPrompt = request.Prompt;
            LastModelRequest = request;
            return Task.FromResult(new ModelResponse { Text = $"stub:{request.Prompt}" });
        }
    }

    [Fact]
    public void ConversationModule_ReceivesIModelClient_ViaDI()
    {
        var services = new ServiceCollection();
        var stub = new StubModelClient();
        services.AddSingleton<IModelClient>(stub);
        services.AddSingleton<ConversationModule>();

        var provider = services.BuildServiceProvider();
        var module = provider.GetRequiredService<ConversationModule>();

        Assert.NotNull(module);
    }

    [Fact]
    public void ConversationModule_UsesInjectedModelClient_InProposal()
    {
        var stub = new StubModelClient();
        var module = new ConversationModule(stub);

        var bus = new UtilityAi.Utils.EventBus();
        bus.Publish(new UtilityAi.Compass.Abstractions.Facts.UserRequest("Hello test"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("conversation.chat", proposals[0].Id);
    }

    [Fact]
    public async Task ConversationModule_Proposal_DelegatesToModelClient()
    {
        var stub = new StubModelClient();
        var module = new ConversationModule(stub);

        var bus = new UtilityAi.Utils.EventBus();
        bus.Publish(new UtilityAi.Compass.Abstractions.Facts.UserRequest("Hello world"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposal = module.Propose(rt).First();
        await proposal.Act(CancellationToken.None);

        Assert.Equal("Hello world", stub.LastPrompt);
        var response = bus.GetOrDefault<UtilityAi.Compass.Abstractions.Facts.AiResponse>();
        Assert.NotNull(response);
        Assert.Equal("stub:Hello world", response!.Text);
    }

    [Fact]
    public async Task ConversationModule_Proposal_SendsSystemMessageAndParameters()
    {
        var stub = new StubModelClient();
        var module = new ConversationModule(stub);

        var bus = new UtilityAi.Utils.EventBus();
        bus.Publish(new UtilityAi.Compass.Abstractions.Facts.UserRequest("Tell me a joke"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposal = module.Propose(rt).First();
        await proposal.Act(CancellationToken.None);

        Assert.NotNull(stub.LastModelRequest);
        Assert.Equal("Tell me a joke", stub.LastModelRequest!.Prompt);
        Assert.Contains("helpful conversational AI assistant", stub.LastModelRequest.SystemMessage);
        Assert.Equal(512, stub.LastModelRequest.MaxTokens);
    }

    [Fact]
    public void ModelRequest_SupportsToolsAndHints()
    {
        var tools = new List<ModelTool>
        {
            new("search", "Search the web", new Dictionary<string, string> { ["query"] = "string" }),
            new("calc", "Do math")
        };

        var request = new ModelRequest
        {
            Prompt = "Find the weather",
            SystemMessage = "You are a helpful assistant",
            ModelHint = "gpt-4o",
            MaxTokens = 256,
            Tools = tools
        };

        Assert.Equal("Find the weather", request.Prompt);
        Assert.Equal(2, request.Tools!.Count);
        Assert.Equal("search", request.Tools[0].Name);
        Assert.Equal("gpt-4o", request.ModelHint);
    }

    [Fact]
    public async Task ConversationModule_Proposal_SetsSystemMessage()
    {
        var stub = new StubModelClient();
        var module = new ConversationModule(stub);

        var bus = new UtilityAi.Utils.EventBus();
        bus.Publish(new UtilityAi.Compass.Abstractions.Facts.UserRequest("Explain utility AI"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposal = module.Propose(rt).First();
        await proposal.Act(CancellationToken.None);

        Assert.NotNull(stub.LastModelRequest);
        Assert.Contains("When you see conversation history", stub.LastModelRequest!.SystemMessage);
    }

    [Fact]
    public void ConversationModule_ReturnsEmpty_WhenExecutionGoalIsSelected()
    {
        var stub = new StubModelClient();
        var module = new ConversationModule(stub);

        var bus = new EventBus();
        bus.Publish(new UserRequest("delete the file"));
        bus.Publish(new GoalSelected(GoalTag.Execute, 0.95, "test"));
        bus.Publish(new LaneSelected(Lane.Execute));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }
}
