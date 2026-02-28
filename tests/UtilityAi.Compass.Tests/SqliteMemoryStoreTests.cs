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
        var dbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "memory.db");
        var store = new SqliteMemoryStore($"Data Source={dbPath}");
        var now = DateTimeOffset.UtcNow;

        await store.StoreAsync(new TestFact("first"), now.AddSeconds(-1));
        await store.StoreAsync(new TestFact("second"), now);

        var recalled = await store.RecallAsync<TestFact>(new MemoryQuery { MaxResults = 10, SortOrder = SortOrder.NewestFirst });

        Assert.Equal(2, await store.CountAsync<TestFact>());
        Assert.Equal(["second", "first"], recalled.Select(r => r.Fact.Value).ToArray());
        Assert.True(File.Exists(dbPath));
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
}
