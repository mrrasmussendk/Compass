using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Runtime.Modules;
using UtilityAi.Memory;

namespace UtilityAi.Compass.Tests;

public class SchedulerServiceTests
{
    [Fact]
    public async Task ExecuteDueJobsAsync_RunsDueJob_AndLogsResult()
    {
        var store = new InMemoryStore();
        var job = new ScheduledJob("job-echo", "echo hello", IntervalSeconds: 1, CreatedAt: DateTimeOffset.UtcNow);
        await store.StoreAsync(job, DateTimeOffset.UtcNow);

        var service = new SchedulerService(store);

        await service.ExecuteDueJobsAsync(CancellationToken.None);

        // Give the fire-and-forget task a moment to complete.
        await Task.Delay(2000);

        var runs = await store.RecallAsync<ScheduledJobRun>(
            new MemoryQuery { MaxResults = 10, SortOrder = SortOrder.NewestFirst });

        Assert.Single(runs);
        Assert.Equal("job-echo", runs[0].Fact.JobId);
        Assert.True(runs[0].Fact.Success);
        Assert.Contains("hello", runs[0].Fact.Output ?? "");
    }

    [Fact]
    public async Task ExecuteDueJobsAsync_SkipsJobNotYetDue()
    {
        var store = new InMemoryStore();
        var job = new ScheduledJob("job-skip", "echo skipped", IntervalSeconds: 600, CreatedAt: DateTimeOffset.UtcNow);
        await store.StoreAsync(job, DateTimeOffset.UtcNow);

        // Simulate a recent run.
        await store.StoreAsync(
            new ScheduledJobRun("job-skip", DateTimeOffset.UtcNow.AddSeconds(-5), Success: true),
            DateTimeOffset.UtcNow);

        var service = new SchedulerService(store);
        await service.ExecuteDueJobsAsync(CancellationToken.None);
        await Task.Delay(500);

        var runs = await store.RecallAsync<ScheduledJobRun>(
            new MemoryQuery { MaxResults = 10, SortOrder = SortOrder.NewestFirst });

        // Only the manually stored run should exist.
        Assert.Single(runs);
    }

    [Fact]
    public async Task ExecuteJobAsync_LogsFailure_WhenCommandFails()
    {
        var store = new InMemoryStore();
        var job = new ScheduledJob("job-fail", "exit 1", IntervalSeconds: 5, CreatedAt: DateTimeOffset.UtcNow);

        var service = new SchedulerService(store);
        await service.ExecuteJobAsync(job);

        var runs = await store.RecallAsync<ScheduledJobRun>(
            new MemoryQuery { MaxResults = 10, SortOrder = SortOrder.NewestFirst });

        Assert.Single(runs);
        Assert.Equal("job-fail", runs[0].Fact.JobId);
        Assert.False(runs[0].Fact.Success);
    }

    [Fact]
    public async Task ExecuteJobAsync_CapturesOutput()
    {
        var store = new InMemoryStore();
        var job = new ScheduledJob("job-output", "echo scheduler_works", IntervalSeconds: 5, CreatedAt: DateTimeOffset.UtcNow);

        var service = new SchedulerService(store);
        await service.ExecuteJobAsync(job);

        var runs = await store.RecallAsync<ScheduledJobRun>(
            new MemoryQuery { MaxResults = 10, SortOrder = SortOrder.NewestFirst });

        Assert.Single(runs);
        Assert.True(runs[0].Fact.Success);
        Assert.Contains("scheduler_works", runs[0].Fact.Output ?? "");
    }

    [Fact]
    public async Task StartAndStop_DoesNotThrow()
    {
        var store = new InMemoryStore();
        var service = new SchedulerService(store, pollInterval: TimeSpan.FromMilliseconds(100));

        service.Start();
        await Task.Delay(300);
        await service.StopAsync();
        service.Dispose();
    }
}
