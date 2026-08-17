namespace HSBGTracker.Core.Model;

public sealed class PlayerState
{
    public int PlayerId { get; init; }

    public int HeroEntityId { get; set; }
    public string? HeroCardId { get; set; }

    /// <summary>BattleTag or DebugPrintGame PlayerName when the log reveals it.</summary>
    public string? DisplayName { get; set; }

    public int TavernTier { get; set; }

    /// <summary>PlayerID of the opponent for the current/next combat. Null when unset.</summary>
    public int? CurrentOpponentPlayerId { get; set; }

    /// <summary>PlayerID of the most recent real combat opponent.</summary>
    public int? LastOpponentPlayerId { get; set; }

    /// <summary>1-8. Set once this player is eliminated; null while still alive.</summary>
    public int? LeaderboardPlace { get; set; }

    /// <summary>Most recent PLAYER_LEADERBOARD_PLACE seen for this player, before debouncing
    /// confirms it as final. Battlegrounds recalculates standings for the whole lobby in a
    /// tight burst every time anyone is knocked out, so this fluctuates - see
    /// GameState.NotifyLeaderboardPlaceChanged.</summary>
    public int? PendingLeaderboardPlace { get; set; }

    public bool IsEliminated => LeaderboardPlace.HasValue;
}
