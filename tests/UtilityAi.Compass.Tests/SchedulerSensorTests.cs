using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Runtime.Sensors;
using UtilityAi.Memory;
using UtilityAi.Utils;
using Rt = UtilityAi.Utils.Runtime;

namespace UtilityAi.Compass.Tests;

public class SchedulerSensorTests
{
    [Fact]
    public async Task SenseAsync_PublishesDueJobs_WhenIntervalElapsed()
    {
        var store = new InMemoryStore();
        var job = new ScheduledJob("job-1", "echo hi", IntervalSeconds: 5, CreatedAt: DateTimeOffset.UtcNow);
        await store.StoreAsync(job, DateTimeOffset.UtcNow);

        // Store a run that happened 10 seconds ago – past the 5-second interval.
        var oldRun = new ScheduledJobRun("job-1", DateTimeOffset.UtcNow.AddSeconds(-10), Success: true);
        await store.StoreAsync(oldRun, DateTimeOffset.UtcNow.AddSeconds(-10));

        var sensor = new SchedulerSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var due = bus.GetOrDefault<ScheduledJobsDue>();
        Assert.NotNull(due);
        Assert.Single(due.DueJobs);
        Assert.Equal("job-1", due.DueJobs[0].JobId);
    }

    [Fact]
    public async Task SenseAsync_DoesNotPublish_WhenIntervalNotElapsed()
    {
        var store = new InMemoryStore();
        var job = new ScheduledJob("job-1", "echo hi", IntervalSeconds: 60, CreatedAt: DateTimeOffset.UtcNow);
        await store.StoreAsync(job, DateTimeOffset.UtcNow);

        // Store a run that happened 5 seconds ago – well within the 60-second interval.
        var recentRun = new ScheduledJobRun("job-1", DateTimeOffset.UtcNow.AddSeconds(-5), Success: true);
        await store.StoreAsync(recentRun, DateTimeOffset.UtcNow);

        var sensor = new SchedulerSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var due = bus.GetOrDefault<ScheduledJobsDue>();
        Assert.Null(due);
    }

    [Fact]
    public async Task SenseAsync_PublishesDueJob_WhenNeverRun()
    {
        var store = new InMemoryStore();
        var job = new ScheduledJob("job-new", "date", IntervalSeconds: 10, CreatedAt: DateTimeOffset.UtcNow);
        await store.StoreAsync(job, DateTimeOffset.UtcNow);

        var sensor = new SchedulerSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var due = bus.GetOrDefault<ScheduledJobsDue>();
        Assert.NotNull(due);
        Assert.Single(due.DueJobs);
        Assert.Equal("job-new", due.DueJobs[0].JobId);
    }

    [Fact]
    public async Task SenseAsync_IgnoresDisabledJobs()
    {
        var store = new InMemoryStore();
        var job = new ScheduledJob("job-off", "echo disabled", IntervalSeconds: 5, CreatedAt: DateTimeOffset.UtcNow, Enabled: false);
        await store.StoreAsync(job, DateTimeOffset.UtcNow);

        var sensor = new SchedulerSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var due = bus.GetOrDefault<ScheduledJobsDue>();
        Assert.Null(due);
    }
}
