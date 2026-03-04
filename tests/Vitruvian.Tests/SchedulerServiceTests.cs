using VitruvianAbstractions.Scheduling;
using VitruvianRuntime.DI;
using VitruvianRuntime.Scheduling;
using Xunit;

namespace VitruvianTests;

public sealed class SchedulerServiceTests
{
    [Fact]
    public async Task RunDueTasksAsync_ExecutesDueTasks()
    {
        var store = new InMemoryScheduledTaskStore();
        var task = new ScheduledTask
        {
            Request = "hello",
            NextRunUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            RepeatInterval = TimeSpan.FromHours(1),
            Enabled = true
        };
        await store.AddAsync(task);

        var executed = new List<string>();
        var options = new VitruvianOptions { SchedulerPollInterval = TimeSpan.FromSeconds(1) };
        var service = new SchedulerService(
            store,
            (request, ct) =>
            {
                executed.Add(request);
                return Task.FromResult("ok");
            },
            options);

        await service.RunDueTasksAsync(CancellationToken.None);

        Assert.Single(executed);
        Assert.Equal("hello", executed[0]);
        Assert.Equal(1, task.RunCount);
        Assert.NotNull(task.LastRunUtc);
        // Next run should be advanced by the repeat interval
        Assert.True(task.NextRunUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task RunDueTasksAsync_OneShotTask_DisablesAfterExecution()
    {
        var store = new InMemoryScheduledTaskStore();
        var task = new ScheduledTask
        {
            Request = "run once",
            NextRunUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            RepeatInterval = null, // one-shot
            Enabled = true
        };
        await store.AddAsync(task);

        var executed = new List<string>();
        var options = new VitruvianOptions();
        var service = new SchedulerService(
            store,
            (request, ct) =>
            {
                executed.Add(request);
                return Task.FromResult("ok");
            },
            options);

        await service.RunDueTasksAsync(CancellationToken.None);

        Assert.Single(executed);
        Assert.False(task.Enabled);
    }

    [Fact]
    public async Task RunDueTasksAsync_NoOverdueTasks_DoesNotExecute()
    {
        var store = new InMemoryScheduledTaskStore();
        var task = new ScheduledTask
        {
            Request = "future",
            NextRunUtc = DateTimeOffset.UtcNow.AddHours(1),
            Enabled = true
        };
        await store.AddAsync(task);

        var executed = new List<string>();
        var options = new VitruvianOptions();
        var service = new SchedulerService(
            store,
            (request, ct) =>
            {
                executed.Add(request);
                return Task.FromResult("ok");
            },
            options);

        await service.RunDueTasksAsync(CancellationToken.None);

        Assert.Empty(executed);
    }

    [Fact]
    public async Task RunDueTasksAsync_FailingTask_ContinuesAndLogs()
    {
        var store = new InMemoryScheduledTaskStore();
        var task = new ScheduledTask
        {
            Request = "will fail",
            NextRunUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            RepeatInterval = TimeSpan.FromHours(1),
            Enabled = true
        };
        await store.AddAsync(task);

        var logs = new List<string>();
        var options = new VitruvianOptions();
        var service = new SchedulerService(
            store,
            (request, ct) => throw new InvalidOperationException("boom"),
            options,
            msg => logs.Add(msg));

        await service.RunDueTasksAsync(CancellationToken.None);

        Assert.Equal(1, task.RunCount);
        Assert.Contains(logs, l => l.Contains("boom"));
    }
}
