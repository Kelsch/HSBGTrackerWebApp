using System.Data;
using Dapper;

namespace HSBGTrackerWebApp.Api.Data;

public static class DatabaseInitializer
{
    public static async Task EnsureCreatedAsync(ISqlConnectionFactory connections)
    {
        using var conn = connections.Create();

        // Optional: only run if the Users table is missing
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'");

        if (exists > 0)
            return;   // already initialized – do nothing

        var sql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Data", "Schema", "sqlite.sql"));

        await conn.ExecuteAsync(sql);
    }
}