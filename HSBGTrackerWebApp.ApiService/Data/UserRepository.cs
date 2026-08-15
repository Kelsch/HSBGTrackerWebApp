using Dapper;
using HSBGTracker.Core.Snapshots;

namespace HSBGTrackerWebApp.Api.Data;

public sealed class UserRecord
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = "";
    public string? BattleTag { get; init; }
    public byte[] ApiKeyHash { get; init; } = Array.Empty<byte>();
    public ResultVisibility DefaultVisibility { get; init; }
}

public interface IUserRepository
{
    Task<Guid> CreateAsync(string displayName, string? battleTag, byte[] apiKeyHash, ResultVisibility defaultVisibility);
    Task<UserRecord?> FindByIdAsync(Guid id);
    Task<UserRecord?> FindByApiKeyHashAsync(byte[] apiKeyHash);
    Task<UserRecord?> FindByBattleTagAsync(string battleTag);
}

public sealed class UserRepository : IUserRepository
{
    private readonly ISqlConnectionFactory _connections;

    public UserRepository(ISqlConnectionFactory connections) => _connections = connections;

    public async Task<Guid> CreateAsync(string displayName, string? battleTag, byte[] apiKeyHash, ResultVisibility defaultVisibility)
    {
        using var conn = _connections.Create();
        var id = Guid.NewGuid();
        await conn.ExecuteAsync(
            """
            INSERT INTO Users (Id, DisplayName, BattleTag, ApiKeyHash, DefaultVisibility)
            VALUES (@Id, @DisplayName, @BattleTag, @ApiKeyHash, @DefaultVisibility)
            """,
            new { Id = id, DisplayName = displayName, BattleTag = battleTag, ApiKeyHash = apiKeyHash, DefaultVisibility = (short)defaultVisibility });
        return id;
    }

    public async Task<UserRecord?> FindByIdAsync(Guid id)
    {
        using var conn = _connections.Create();
        return await conn.QuerySingleOrDefaultAsync<UserRecord>(
            "SELECT Id, DisplayName, BattleTag, ApiKeyHash, DefaultVisibility FROM Users WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<UserRecord?> FindByApiKeyHashAsync(byte[] apiKeyHash)
    {
        using var conn = _connections.Create();
        return await conn.QuerySingleOrDefaultAsync<UserRecord>(
            "SELECT Id, DisplayName, BattleTag, ApiKeyHash, DefaultVisibility FROM Users WHERE ApiKeyHash = @ApiKeyHash",
            new { ApiKeyHash = apiKeyHash });
    }

    public async Task<UserRecord?> FindByBattleTagAsync(string battleTag)
    {
        using var conn = _connections.Create();
        return await conn.QuerySingleOrDefaultAsync<UserRecord>(
            "SELECT Id, DisplayName, BattleTag, ApiKeyHash, DefaultVisibility FROM Users WHERE BattleTag = @BattleTag",
            new { BattleTag = battleTag });
    }
}
