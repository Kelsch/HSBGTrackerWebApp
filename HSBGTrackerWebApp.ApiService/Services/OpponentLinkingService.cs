using HSBGTrackerWebApp.Api.Data;

namespace HSBGTrackerWebApp.Api.Services;

public interface IOpponentLinkingService
{
    Task<Guid?> TryLinkAsync(string opponentPlayerName);
}

/// <summary>
/// Matches an opponent's captured BattleTag against the Users table, so their board can
/// link back to their own account when they're also a friend using the app.
/// </summary>
public sealed class OpponentLinkingService : IOpponentLinkingService
{
    private readonly IUserRepository _users;

    public OpponentLinkingService(IUserRepository users) => _users = users;

    public async Task<Guid?> TryLinkAsync(string opponentPlayerName)
    {
        if (string.IsNullOrWhiteSpace(opponentPlayerName))
            return null;

        var match = await _users.FindByBattleTagAsync(opponentPlayerName);
        return match?.Id;
    }
}
