namespace HSBGTracker.Core.Snapshots;

/// <summary>
/// One row per Battlegrounds game. BoardSnapshots are stored as JSON columns (MyBoardJson /
/// OpponentBoardJson) rather than normalized tables - simplest to write with Dapper, and you
/// almost always want the whole board back together rather than querying into individual
/// minions. Normalize later into a Minions table if you end up wanting per-card analytics
/// (e.g. "how often does Card X show up on boards that beat me").
/// </summary>
public enum ResultVisibility
{
    /// <summary>Uploaded and stored, but only visible to its owner when browsing results.</summary>
    Private = 0,

    /// <summary>Visible to the whole friend group.</summary>
    Public = 1,
}

public sealed class BattlegroundsResultRecord
{
    public string GameId { get; set; } = "";

    /// <summary>Whose game this is - drives the "just show my games" filter.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>Defaults to the owner's account preference at upload time, but can be
    /// overridden per game if someone wants one specific result kept to themselves.</summary>
    public ResultVisibility Visibility { get; set; } = ResultVisibility.Public;

    public DateTime PlayedAtUtc { get; set; }
    public int Placement { get; set; }

    public string MyBoardJson { get; set; } = "";
    public double? MyBoardScore { get; set; }

    /// <summary>
    /// The board from your final combat of the game - whoever you beat for 1st place,
    /// or whoever eliminated you. Always populated; a Battlegrounds game always ends
    /// with exactly one final combat between two remaining players.
    /// </summary>
    public string OpponentBoardJson { get; set; } = "";
    public double? OpponentBoardScore { get; set; }

    /// <summary>Opponent's display name/BattleTag, always set.</summary>
    public string OpponentPlayerName { get; set; } = "";

    /// <summary>
    /// Set only if the opponent is recognized as another friend using the app (matched by
    /// identity on upload), so their board can link back to their own account/history.
    /// Null when the opponent is a stranger - their board/name are still stored above,
    /// just with nothing to link to.
    /// </summary>
    public Guid? OpponentOwnerUserId { get; set; }
}
