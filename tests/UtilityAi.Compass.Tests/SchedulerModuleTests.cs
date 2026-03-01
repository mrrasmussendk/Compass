using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Runtime.Modules;
using UtilityAi.Compass.Runtime.Sensors;
using UtilityAi.Memory;
using UtilityAi.Utils;
using Rt = UtilityAi.Utils.Runtime;

namespace UtilityAi.Compass.Tests;

public class SchedulerModuleTests
{
    [Fact]
    public void IsScheduleRequest_ReturnsTrueForScheduleKeyword()
    {
        Assert.True(SchedulerModule.IsScheduleRequest("schedule 'echo hello' every 60s"));
    }

    [Fact]
    public void IsScheduleRequest_ReturnsTrueForEveryKeyword()
    {
        Assert.True(SchedulerModule.IsScheduleRequest("run 'ls' every 30"));
    }

    [Fact]
    public void IsScheduleRequest_ReturnsTrueForIntervalKeyword()
    {
        Assert.True(SchedulerModule.IsScheduleRequest("set interval for 'date' 120"));
    }

    [Fact]
    public void IsScheduleRequest_ReturnsFalseForUnrelatedText()
    {
        Assert.False(SchedulerModule.IsScheduleRequest("read file test.txt"));
    }

    [Fact]
    public void ParseScheduleRequest_ExtractsCommandAndInterval()
    {
        var (command, interval) = SchedulerModule.ParseScheduleRequest("schedule 'echo hello' every 60s");
        Assert.Equal("echo hello", command);
        Assert.Equal(60, interval);
    }

    [Fact]
    public void ParseScheduleRequest_HandlesDoubleQuotes()
    {
        var (command, interval) = SchedulerModule.ParseScheduleRequest("schedule \"ls -la\" every 30");
        Assert.Equal("ls -la", command);
        Assert.Equal(30, interval);
    }

    [Fact]
    public void ParseScheduleRequest_DefaultsTo60SecondsWhenNoIntervalGiven()
    {
        var (command, interval) = SchedulerModule.ParseScheduleRequest("schedule 'date'");
        Assert.Equal("date", command);
        Assert.Equal(60, interval);
    }

    [Fact]
    public void Propose_YieldsProposal_WhenRequestContainsScheduleKeyword()
    {
        var store = new InMemoryStore();
        var module = new SchedulerModule(store);

        var bus = new EventBus();
        bus.Publish(new UserRequest("schedule 'echo test' every 10s"));
        var rt = new Rt(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("scheduler.add", proposals[0].Id);
    }

    [Fact]
    public void Propose_YieldsNothing_WhenNoUserRequest()
    {
        var store = new InMemoryStore();
        var module = new SchedulerModule(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public void Propose_YieldsNothing_WhenRequestIsUnrelated()
    {
        var store = new InMemoryStore();
        var module = new SchedulerModule(store);
        var bus = new EventBus();
        bus.Publish(new UserRequest("read file test.txt"));
        var rt = new Rt(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public async Task ProposalAction_PersistsJobAndPublishesResponse()
    {
        var store = new InMemoryStore();
        var module = new SchedulerModule(store);
        var bus = new EventBus();
        bus.Publish(new UserRequest("schedule 'echo hello' every 30s"));
        var rt = new Rt(bus, 0);

        var proposal = module.Propose(rt).Single();
        await proposal.Act(CancellationToken.None);

        var jobs = await store.RecallAsync<ScheduledJob>(new MemoryQuery { MaxResults = 10 });
        Assert.Single(jobs);
        Assert.Equal("echo hello", jobs[0].Fact.Command);
        Assert.Equal(30, jobs[0].Fact.IntervalSeconds);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        Assert.Contains("echo hello", response.Text);

        var added = bus.GetOrDefault<ScheduledJobAdded>();
        Assert.NotNull(added);
        Assert.Equal("echo hello", added.Job.Command);
    }
}
