using HSBGTracker.Core.Snapshots;

namespace HSBGTracker.Core.Contracts;

public sealed class GameDto
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }
    public string OwnerDisplayName { get; set; } = "";

    public ResultVisibility Visibility { get; set; }
    public DateTime PlayedAtUtc { get; set; }
    public int Placement { get; set; }

    public BoardSnapshot MyBoard { get; set; } = new();
    public double? MyBoardScore { get; set; }

    public BoardSnapshot OpponentBoard { get; set; } = new();
    public double? OpponentBoardScore { get; set; }
    public string OpponentPlayerName { get; set; } = "";

    /// <summary>Set when the opponent is a recognized friend account - lets the UI render
    /// their board as a link back to their own history instead of just a name.</summary>
    public Guid? OpponentOwnerUserId { get; set; }
    public string? OpponentOwnerDisplayName { get; set; }
}
