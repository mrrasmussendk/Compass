using VitruvianAbstractions.Scheduling;
using VitruvianRuntime.Scheduling;
using Xunit;

namespace VitruvianTests;

public sealed class InMemoryScheduledTaskStoreTests
{
    [Fact]
    public async Task AddAsync_StoresTask()
    {
        var store = new InMemoryScheduledTaskStore();
        var task = new ScheduledTask { Request = "say hello" };

        await store.AddAsync(task);

        var all = await store.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("say hello", all[0].Request);
    }

    [Fact]
    public async Task RemoveAsync_ExistingTask_ReturnsTrue()
    {
        var store = new InMemoryScheduledTaskStore();
        var task = new ScheduledTask { Request = "say hello" };
        await store.AddAsync(task);

        var removed = await store.RemoveAsync(task.Id);

        Assert.True(removed);
        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task RemoveAsync_NonExistent_ReturnsFalse()
    {
        var store = new InMemoryScheduledTaskStore();

        var removed = await store.RemoveAsync("does-not-exist");

        Assert.False(removed);
    }

    [Fact]
    public async Task GetDueTasksAsync_ReturnsDueEnabledTasks()
    {
        var store = new InMemoryScheduledTaskStore();
        var now = DateTimeOffset.UtcNow;

        var dueTask = new ScheduledTask { Request = "due", NextRunUtc = now.AddMinutes(-1), Enabled = true };
        var futureTask = new ScheduledTask { Request = "future", NextRunUtc = now.AddHours(1), Enabled = true };
        var disabledTask = new ScheduledTask { Request = "disabled", NextRunUtc = now.AddMinutes(-1), Enabled = false };

        await store.AddAsync(dueTask);
        await store.AddAsync(futureTask);
        await store.AddAsync(disabledTask);

        var due = await store.GetDueTasksAsync(now);

        Assert.Single(due);
        Assert.Equal("due", due[0].Request);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllTasks()
    {
        var store = new InMemoryScheduledTaskStore();
        await store.AddAsync(new ScheduledTask { Request = "one" });
        await store.AddAsync(new ScheduledTask { Request = "two" });

        var all = await store.GetAllAsync();

        Assert.Equal(2, all.Count);
    }
}
