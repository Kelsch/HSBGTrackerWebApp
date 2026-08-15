using HSBGTracker.Core.Snapshots;

namespace HSBGTrackerWebApp.Api.Contracts;

/// <summary>
/// Upload payload for a finished game. OwnerUserId isn't part of this - it comes from the
/// authenticated caller, never from the request body.
/// </summary>
public sealed class UploadGameRequest
{
    /// <summary>Hearthstone's own game id, from the log - used server-side to dedupe retried uploads.</summary>
    public string ClientGameId { get; set; } = "";

    /// <summary>Null = use the caller's account default visibility.</summary>
    public ResultVisibility? Visibility { get; set; }

    public DateTime PlayedAtUtc { get; set; }
    public int Placement { get; set; }

    public BoardSnapshot MyBoard { get; set; } = new();

    /// <summary>The final combat opponent's board - whoever this player beat for 1st, or whoever eliminated them.</summary>
    public BoardSnapshot OpponentBoard { get; set; } = new();

    /// <summary>Opponent's BattleTag as captured from the log - used to try to cross-link to a friend's account.</summary>
    public string OpponentPlayerName { get; set; } = "";
}
