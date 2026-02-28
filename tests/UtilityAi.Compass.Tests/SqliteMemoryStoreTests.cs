using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Compass.Runtime.DI;
using UtilityAi.Compass.Runtime.Memory;
using UtilityAi.Memory;

namespace UtilityAi.Compass.Tests;

public class SqliteMemoryStoreTests
{
    private sealed record TestFact(string Value);

    [Fact]
    public async Task StoreAndRecallAsync_PersistsFactsToDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(root, "memory.db");
        try
        {
            var store = new SqliteMemoryStore($"Data Source={dbPath};Pooling=False");
            var now = DateTimeOffset.UtcNow;

            await store.StoreAsync(new TestFact("first"), now.AddSeconds(-1));
            await store.StoreAsync(new TestFact("second"), now);

            var recalled = await store.RecallAsync<TestFact>(new MemoryQuery { MaxResults = 10, SortOrder = SortOrder.NewestFirst });

            Assert.Equal(2, await store.CountAsync<TestFact>());
            Assert.Equal(["second", "first"], recalled.Select(r => r.Fact.Value).ToArray());
            Assert.True(File.Exists(dbPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AddUtilityAiCompass_RegistersSqliteMemoryStoreByDefault()
    {
        var services = new ServiceCollection();
        services.AddUtilityAiCompass();
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<IMemoryStore>();

        Assert.IsType<SqliteMemoryStore>(store);
    }

    [Fact]
    public async Task RecallAndPruneAsync_AppliesTimeFiltersAndRetention()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(root, "memory.db");
        try
        {
            var store = new SqliteMemoryStore($"Data Source={dbPath};Pooling=False");
            var now = DateTimeOffset.UtcNow;

            await store.StoreAsync(new TestFact("old"), now.AddMinutes(-10));
            await store.StoreAsync(new TestFact("recent"), now.AddMinutes(-1));

            var beforeQuery = await store.RecallAsync<TestFact>(new MemoryQuery { MaxResults = 10, Before = now.AddMinutes(-5) });
            Assert.Single(beforeQuery);
            Assert.Equal("old", beforeQuery[0].Fact.Value);

            var windowQuery = await store.RecallAsync<TestFact>(new MemoryQuery { MaxResults = 10, TimeWindow = TimeSpan.FromMinutes(2) });
            Assert.Single(windowQuery);
            Assert.Equal("recent", windowQuery[0].Fact.Value);

            await store.PruneAsync(TimeSpan.FromMinutes(2));
            Assert.Equal(1, await store.CountAsync<TestFact>());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
