namespace HSBGTracker.Core.Model;

public sealed class PlayerState
{
    public int PlayerId { get; init; }

    public int HeroEntityId { get; set; }
    public string? HeroCardId { get; set; }

    public int TavernTier { get; set; }

    /// <summary>1-8. Set once this player is eliminated; null while still alive.</summary>
    public int? LeaderboardPlace { get; set; }

    /// <summary>Most recent PLAYER_LEADERBOARD_PLACE seen for this player, before debouncing
    /// confirms it as final. Battlegrounds recalculates standings for the whole lobby in a
    /// tight burst every time anyone is knocked out, so this fluctuates - see
    /// GameState.NotifyLeaderboardPlaceChanged.</summary>
    public int? PendingLeaderboardPlace { get; set; }

    public bool IsEliminated => LeaderboardPlace.HasValue;
}
