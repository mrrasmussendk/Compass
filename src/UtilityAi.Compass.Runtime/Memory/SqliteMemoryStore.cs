using System.Text.Json;
using Microsoft.Data.Sqlite;
using UtilityAi.Memory;

namespace UtilityAi.Compass.Runtime.Memory;

/// <summary>
/// SQLite-backed implementation of <see cref="IMemoryStore"/> for durable Compass memory.
/// </summary>
public sealed class SqliteMemoryStore : IMemoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteMemoryStore"/> class.
    /// </summary>
    /// <param name="connectionString">SQLite connection string.</param>
    public SqliteMemoryStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (!string.IsNullOrWhiteSpace(builder.DataSource)
            && !string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            builder.DataSource = Path.GetFullPath(builder.DataSource);
            var directory = Path.GetDirectoryName(builder.DataSource);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
        }

        _connectionString = builder.ToString();
        EnsureSchema();
    }

    public async Task StoreAsync<T>(T fact, DateTimeOffset timestamp, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(fact);

        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CompassMemoryEntries (TypeKey, TimestampUnixMs, PayloadJson)
            VALUES ($typeKey, $timestampUnixMs, $payloadJson);
            """;
        command.Parameters.AddWithValue("$typeKey", GetTypeKey<T>());
        command.Parameters.AddWithValue("$timestampUnixMs", timestamp.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$payloadJson", JsonSerializer.Serialize(fact, SerializerOptions));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<TimestampedMemory<T>>> RecallAsync<T>(
        MemoryQuery query,
        CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);

        var after = query.TimeWindow.HasValue ? DateTimeOffset.UtcNow - query.TimeWindow.Value : query.After;
        var before = query.TimeWindow.HasValue ? (DateTimeOffset?)null : query.Before;
        var maxResults = Math.Max(query.MaxResults, 0);
        var commandText = query.SortOrder switch
        {
            SortOrder.NewestFirst => """
                SELECT PayloadJson, TimestampUnixMs
                FROM CompassMemoryEntries
                WHERE TypeKey = $typeKey
                  AND ($afterUnixMs IS NULL OR TimestampUnixMs >= $afterUnixMs)
                  AND ($beforeUnixMs IS NULL OR TimestampUnixMs <= $beforeUnixMs)
                ORDER BY TimestampUnixMs DESC
                LIMIT $maxResults;
                """,
            _ => """
                SELECT PayloadJson, TimestampUnixMs
                FROM CompassMemoryEntries
                WHERE TypeKey = $typeKey
                  AND ($afterUnixMs IS NULL OR TimestampUnixMs >= $afterUnixMs)
                  AND ($beforeUnixMs IS NULL OR TimestampUnixMs <= $beforeUnixMs)
                ORDER BY TimestampUnixMs ASC
                LIMIT $maxResults;
                """
        };

        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("$typeKey", GetTypeKey<T>());
        command.Parameters.AddWithValue("$afterUnixMs", after?.ToUnixTimeMilliseconds() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$beforeUnixMs", before?.ToUnixTimeMilliseconds() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$maxResults", maxResults);

        await using var reader = await command.ExecuteReaderAsync(ct);
        var entries = new List<TimestampedMemory<T>>();
        while (await reader.ReadAsync(ct))
        {
            var json = reader.GetString(0);
            var fact = JsonSerializer.Deserialize<T>(json, SerializerOptions);
            if (fact is null)
                continue;

            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1));
            entries.Add(new TimestampedMemory<T>(fact, timestamp));
        }

        return entries;
    }

    public async Task<int> CountAsync<T>(CancellationToken ct = default) where T : class
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM CompassMemoryEntries
            WHERE TypeKey = $typeKey;
            """;
        command.Parameters.AddWithValue("$typeKey", GetTypeKey<T>());
        var count = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt32(count);
    }

    public async Task PruneAsync(TimeSpan retentionPeriod, CancellationToken ct = default)
    {
        var cutoffUnixMs = (DateTimeOffset.UtcNow - retentionPeriod).ToUnixTimeMilliseconds();
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM CompassMemoryEntries
            WHERE TimestampUnixMs < $cutoffUnixMs;
            """;
        command.Parameters.AddWithValue("$cutoffUnixMs", cutoffUnixMs);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string GetTypeKey<T>() => typeof(T).AssemblyQualifiedName ?? throw new InvalidOperationException("Type key could not be resolved.");

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS CompassMemoryEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TypeKey TEXT NOT NULL,
                TimestampUnixMs INTEGER NOT NULL,
                PayloadJson TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_CompassMemoryEntries_TypeKey_TimestampUnixMs
            ON CompassMemoryEntries (TypeKey, TimestampUnixMs);
            """;
        command.ExecuteNonQuery();
    }
}
