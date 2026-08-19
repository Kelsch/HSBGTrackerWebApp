using Dapper;
using HSBGTracker.Core.Snapshots;

namespace HSBGTrackerWebApp.Api.Data;

public sealed class GameRecord
{
    public Guid Id { get; init; }
    public string ClientGameId { get; init; } = "";
    public Guid OwnerUserId { get; init; }
    public string OwnerDisplayName { get; init; } = "";
    public ResultVisibility Visibility { get; init; }
    public DateTime PlayedAtUtc { get; init; }
    public int Placement { get; init; }

    public string MyBoardJson { get; init; } = "";
    public double? MyBoardScore { get; init; }

    public string OpponentBoardJson { get; init; } = "";
    public double? OpponentBoardScore { get; init; }
    public string OpponentPlayerName { get; init; } = "";
    public Guid? OpponentOwnerUserId { get; init; }
    public string? OpponentOwnerDisplayName { get; init; }
}

/// <param name="RequestingUserId">Whoever is browsing - drives the "public OR mine" visibility rule.</param>
/// <param name="OwnerUserId">Optional - set to filter down to one person's games ("just show my games").</param>
public sealed record GameListFilter(Guid RequestingUserId, Guid? OwnerUserId, int Page = 1, int PageSize = 25);

public interface IGameRepository
{
    Task<Guid> UpsertAsync(GameRecord game);
    Task<GameRecord?> GetByIdAsync(Guid id, Guid requestingUserId);
    Task<IReadOnlyList<GameRecord>> ListAsync(GameListFilter filter);
}

public sealed class GameRepository : IGameRepository
{
    private const string SelectWithOwnerNames = """
        SELECT g.*, ou.DisplayName AS OwnerDisplayName, oo.DisplayName AS OpponentOwnerDisplayName
        FROM Games g
        JOIN Users ou ON ou.Id = g.OwnerUserId
        LEFT JOIN Users oo ON oo.Id = g.OpponentOwnerUserId
        """;

    private readonly ISqlConnectionFactory _connections;

    public GameRepository(ISqlConnectionFactory connections) => _connections = connections;

    public async Task<Guid> UpsertAsync(GameRecord game)
    {
        using var conn = _connections.Create();

        // Re-uploading the same (OwnerUserId, ClientGameId) pair is a no-op rather than a
        // duplicate row or an error - the client can safely retry a failed upload.
        var existingId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT Id FROM Games WHERE OwnerUserId = @OwnerUserId AND ClientGameId = @ClientGameId",
            new { game.OwnerUserId, game.ClientGameId });

        if (existingId is not null)
            return existingId.Value;

        var id = Guid.NewGuid();
        await conn.ExecuteAsync(
            """
            INSERT INTO Games (
                Id, ClientGameId, OwnerUserId, Visibility, PlayedAtUtc, Placement,
                MyBoardJson, MyBoardScore, OpponentBoardJson, OpponentBoardScore,
                OpponentPlayerName, OpponentOwnerUserId)
            VALUES (
                @Id, @ClientGameId, @OwnerUserId, @Visibility, @PlayedAtUtc, @Placement,
                @MyBoardJson, @MyBoardScore, @OpponentBoardJson, @OpponentBoardScore,
                @OpponentPlayerName, @OpponentOwnerUserId)
            """,
            new
            {
                Id = id,
                game.ClientGameId,
                game.OwnerUserId,
                Visibility = (short)game.Visibility,
                game.PlayedAtUtc,
                game.Placement,
                game.MyBoardJson,
                game.MyBoardScore,
                game.OpponentBoardJson,
                game.OpponentBoardScore,
                game.OpponentPlayerName,
                game.OpponentOwnerUserId,
            });
        return id;
    }

    public async Task<GameRecord?> GetByIdAsync(Guid id, Guid requestingUserId)
    {
        using var conn = _connections.Create();
        return await conn.QuerySingleOrDefaultAsync<GameRecord>(
            SelectWithOwnerNames + """

            WHERE g.Id = @Id AND (g.Visibility = 1 OR g.OwnerUserId = @RequestingUserId)
            """,
            new { Id = id, RequestingUserId = requestingUserId });
    }

    public async Task<IReadOnlyList<GameRecord>> ListAsync(GameListFilter filter)
    {
        using var conn = _connections.Create();
        var rows = await conn.QueryAsync<GameRecord>(
            SelectWithOwnerNames + """

            WHERE (g.Visibility = 1 OR g.OwnerUserId = @RequestingUserId)
              AND (
                    (@OwnerUserId IS NULL AND g.Placement = 1)
                 OR (@OwnerUserId IS NOT NULL AND g.OwnerUserId = @OwnerUserId)
              )
            ORDER BY PlayedAtUtc DESC
            LIMIT @PageSize OFFSET @Offset
            """,
            new
            {
                filter.RequestingUserId,
                filter.OwnerUserId,
                Offset = (filter.Page - 1) * filter.PageSize,
                filter.PageSize,
            });
        return rows.ToList();
    }
}
