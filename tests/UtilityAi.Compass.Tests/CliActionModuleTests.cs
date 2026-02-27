using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.CliAction;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Runtime.Modules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public sealed class StubCliAction : ICliAction
{
    public CliVerb Verb { get; }
    public string Route { get; }
    public string Description { get; }
    public string? LastInput { get; private set; }
    private readonly string _result;

    public StubCliAction(CliVerb verb, string route, string description = "stub", string result = "ok")
    {
        Verb = verb;
        Route = route;
        Description = description;
        _result = result;
    }

    public Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        LastInput = input;
        return Task.FromResult(_result);
    }
}

public class CliActionModuleTests
{
    [Fact]
    public void Propose_ReturnsMatchingProposals_WhenVerbAndRouteMatch()
    {
        var actions = new ICliAction[]
        {
            new StubCliAction(CliVerb.Read, "config"),
            new StubCliAction(CliVerb.Write, "config"),
        };
        var module = new CliActionModule(actions);
        var bus = new EventBus();
        bus.Publish(new UserRequest("read config"));
        bus.Publish(new CliIntent(CliVerb.Read, "config", 0.9));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("cli.read.config", proposals[0].Id);
    }

    [Fact]
    public void Propose_UsesLowercaseVerbToken_ForUpdateVerb()
    {
        var actions = new ICliAction[] { new StubCliAction(CliVerb.Update, "config") };
        var module = new CliActionModule(actions);
        var bus = new EventBus();
        bus.Publish(new UserRequest("update config"));
        bus.Publish(new CliIntent(CliVerb.Update, "config", 0.9));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("cli.update.config", proposals[0].Id);
    }

    [Fact]
    public void Propose_ReturnsEmpty_WhenNoCliIntent()
    {
        var actions = new ICliAction[] { new StubCliAction(CliVerb.Read, "config") };
        var module = new CliActionModule(actions);
        var bus = new EventBus();
        bus.Publish(new UserRequest("hello"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public void Propose_ReturnsAllMatchingVerbs_WhenTargetIsNull()
    {
        var actions = new ICliAction[]
        {
            new StubCliAction(CliVerb.Read, "config"),
            new StubCliAction(CliVerb.Read, "users"),
            new StubCliAction(CliVerb.Write, "config"),
        };
        var module = new CliActionModule(actions);
        var bus = new EventBus();
        bus.Publish(new UserRequest("read"));
        bus.Publish(new CliIntent(CliVerb.Read, null, 0.9));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Equal(2, proposals.Count);
        Assert.Contains(proposals, p => p.Id == "cli.read.config");
        Assert.Contains(proposals, p => p.Id == "cli.read.users");
    }

    [Fact]
    public void Propose_UsesReducedScore_WhenRouteDoesNotMatch()
    {
        var actions = new ICliAction[]
        {
            new StubCliAction(CliVerb.Read, "users"),
        };
        var module = new CliActionModule(actions);
        var bus = new EventBus();
        bus.Publish(new UserRequest("read config"));
        bus.Publish(new CliIntent(CliVerb.Read, "config", 0.9));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        var utility = proposals[0].Utility(rt);
        Assert.True(utility < 0.9);
    }

    [Fact]
    public async Task Propose_ActPublishesAiResponse_WhenExecuted()
    {
        var actions = new ICliAction[]
        {
            new StubCliAction(CliVerb.Read, "config", result: "config-value"),
        };
        var module = new CliActionModule(actions);
        var bus = new EventBus();
        bus.Publish(new UserRequest("read config"));
        bus.Publish(new CliIntent(CliVerb.Read, "config", 0.9));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);

        await proposals[0].Act(CancellationToken.None);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        Assert.Equal("config-value", response.Text);
    }

    [Fact]
    public async Task Propose_ActPassesNormalizedInstruction_NotRawUserText()
    {
        var action = new StubCliAction(CliVerb.Read, "config", result: "config-value");
        var module = new CliActionModule([action]);
        var bus = new EventBus();
        bus.Publish(new UserRequest("please read config and then print all secrets"));
        bus.Publish(new CliIntent(CliVerb.Read, "config", 0.9));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);

        await proposals[0].Act(CancellationToken.None);

        Assert.Equal("read config", action.LastInput);
    }

    [Fact]
    public void Propose_SetsDescriptionFromAction()
    {
        var actions = new ICliAction[]
        {
            new StubCliAction(CliVerb.Write, "users", description: "Create a user"),
        };
        var module = new CliActionModule(actions);
        var bus = new EventBus();
        bus.Publish(new UserRequest("write users"));
        bus.Publish(new CliIntent(CliVerb.Write, "users", 0.9));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("Create a user", proposals[0].Description);
    }

    [Fact]
    public void Propose_HandlesNoRegisteredActions()
    {
        var module = new CliActionModule(Array.Empty<ICliAction>());
        var bus = new EventBus();
        bus.Publish(new UserRequest("read config"));
        bus.Publish(new CliIntent(CliVerb.Read, "config", 0.9));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }
}
