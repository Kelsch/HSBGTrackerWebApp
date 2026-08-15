using HSBGTracker.Core.Model;

public sealed class GameState
{
    public string? GameId { get; set; }
    public Dictionary<int, Entity> Entities { get; } = new();
    public Dictionary<int, PlayerState> Players { get; } = new();

    private int? _friendlyPlayerId;
    public int? FriendlyPlayerId
    {
        get => _friendlyPlayerId;
        set
        {
            if (_friendlyPlayerId == value) return;
            _friendlyPlayerId = value;
            if (value is not null) FriendlyPlayerIdentified?.Invoke(value.Value);
        }
    }

    public event Action<int>? FriendlyPlayerIdentified;
    public event Action<PlayerState>? PlayerEliminated;

    private readonly Dictionary<int, List<Entity>> _lastKnownBoards = new();
    private readonly Dictionary<int, List<Entity>> _lastKnownAttachments = new();

    /// <summary>Call after any tag mutation that could affect board state. Cheap no-op unless
    /// the player currently has minions in PLAY. Clones the live board so the snapshot survives
    /// later combat/end-of-game cleanup that mutates the same entities out of PLAY.</summary>
    public void RefreshLastKnownBoard(int playerId)
    {
        if (playerId == 0) return;

        var board = GetBoard(playerId);
        if (board.Count == 0) return;

        _lastKnownBoards[playerId] = board.Select(e => e.Clone()).ToList();

        var boardIds = board.Select(e => e.Id).ToHashSet();
        // Enchantments / Dark Gifts / buffs attached to those minions (any zone - end-of-game moves them).
        _lastKnownAttachments[playerId] = Entities.Values
            .Where(e => e.AttachedToEntityId != 0 && boardIds.Contains(e.AttachedToEntityId))
            .Select(e => e.Clone())
            .ToList();
    }

    /// <summary>Prefer this over GetBoard for anything taken at/after game end - falls back to a
    /// live GetBoard if no cached board was ever captured (e.g. player had an empty board).</summary>
    public IReadOnlyList<Entity> GetFinalBoard(int playerId) => _lastKnownBoards.TryGetValue(playerId, out var cached) ? cached : GetBoard(playerId);

    /// <summary>Attachments (enchantments, Dark Gifts, etc.) for the final board minions.</summary>
    public IReadOnlyList<Entity> GetFinalAttachments(int playerId)
    {
        if (_lastKnownAttachments.TryGetValue(playerId, out var cached))
            return cached;

        var boardIds = GetBoard(playerId).Select(e => e.Id).ToHashSet();
        return Entities.Values
            .Where(e => e.AttachedToEntityId != 0 && boardIds.Contains(e.AttachedToEntityId))
            .ToList();
    }

    /// <summary>Live enchantments currently attached to a specific entity id.</summary>
    public IReadOnlyList<Entity> GetAttachments(int hostEntityId) =>
        Entities.Values.Where(e => e.AttachedToEntityId == hostEntityId).ToList();

    /// <summary>EntityID -> PlayerID, built from "Player EntityID=X PlayerID=Y" lines at game
    /// start. CONTROLLER tag values are EntityIDs; everything else (bracket "player=N",
    /// PLAYER_LEADERBOARD_PLACE, FriendlyPlayerId) uses PlayerID - see PlayerMappingPacket.</summary>
    private readonly Dictionary<int, int> _entityIdToPlayerId = new();

    /// <summary>BattleTag / display name ("DalTron#11868", "Bartender Bob") -> player EntityID.
    /// Power.log references players by bare name on many TAG_CHANGEs (including PLAYSTATE and
    /// PLAYER_LEADERBOARD_PLACE); without this those lines are dropped entirely.</summary>
    private readonly Dictionary<string, int> _nameToEntityId =
        new(StringComparer.OrdinalIgnoreCase);

    public void RegisterPlayerMapping(int entityId, int playerId) =>
        _entityIdToPlayerId[entityId] = playerId;

    public void RegisterPlayerName(string name, int entityId)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _nameToEntityId[name] = entityId;
    }

    public int? TranslateControllerEntityId(int entityId) =>
        _entityIdToPlayerId.TryGetValue(entityId, out var playerId) ? playerId : null;

    /// <summary>Resolve a bare log token (BattleTag, "GameEntity", numeric id already handled
    /// by EntityRef) to a player EntityID.</summary>
    public int? ResolvePlayerEntityIdByName(string name) =>
        _nameToEntityId.TryGetValue(name, out var id) ? id : null;

    public void Reset()
    {
        Entities.Clear();
        Players.Clear();
        GameId = null;
        FriendlyPlayerId = null;
        _entityIdToPlayerId.Clear();
        _nameToEntityId.Clear();
        _lastKnownBoards.Clear();
        _lastKnownAttachments.Clear();
    }

    public Entity GetOrCreateEntity(int id)
    {
        if (!Entities.TryGetValue(id, out var entity))
        {
            entity = new Entity(id);
            Entities[id] = entity;
        }
        return entity;
    }

    public PlayerState GetOrCreatePlayer(int playerId)
    {
        if (!Players.TryGetValue(playerId, out var player))
        {
            player = new PlayerState { PlayerId = playerId };
            Players[playerId] = player;
        }
        return player;
    }

    public bool IsFriendlyPlayer(int playerId) => playerId == FriendlyPlayerId;

    public IReadOnlyList<Entity> GetBoard(int playerId) =>
        Entities.Values.Where(e => e.ControllerPlayerId == playerId && e.IsMinionOnBoard)
            .OrderBy(e => e.ZonePosition).ToList();

    public IReadOnlyList<Entity> GetTrinkets(int playerId) =>
        Entities.Values.Where(e => e.ControllerPlayerId == playerId && e.CardType == CardType.TRINKET && e.Zone == Zone.PLAY)
            .ToList();

    public Entity? GetHeroPower(int playerId) =>
        Entities.Values.FirstOrDefault(e => e.ControllerPlayerId == playerId && e.CardType == CardType.HERO_POWER);

    /// <summary>
    /// PLAYER_TECH_LEVEL on the player or hero entity. Tavern never goes down, so keep the high-water mark
    /// in case end-of-game cleanup writes a 0.
    /// </summary>
    public void NotifyTavernTierChanged(int playerId, int tier)
    {
        if (playerId == 0 || playerId == 10)
        {
            return;
        }
        if (tier <= 0)
        {
            return;
        }

        var player = GetOrCreatePlayer(playerId);
        if (tier > player.TavernTier)
        {
            player.TavernTier = tier;
        }
    }

    /// <summary>Best-effort read if PlayerState was never updated (tag lived only on an entity).</summary>
    public int ResolveTavernTier(int playerId)
    {
        var player = GetOrCreatePlayer(playerId);
        if (player.TavernTier > 0)
        {
            return player.TavernTier;
        }

        var fromEntities = 0;
        foreach (var e in Entities.Values)
        {
            if (e.ControllerPlayerId != playerId && TranslateControllerEntityId(e.Id) != playerId)
            {
                continue;
            }
            if (e.CardType is not (CardType.PLAYER or CardType.HERO))
            {
                continue;
            }

            var tier = e.GetTag(GameTag.PLAYER_TECH_LEVEL);
            if (tier > fromEntities)
            {
                fromEntities = tier;
            }
        }

        return fromEntities;
    }

    /// <summary>
    /// While alive this is current standing (noisy). After the run ends, BG often
    /// still writes the final place a moment later - keep accepting those updates.
    /// </summary>
    public void NotifyLeaderboardPlaceChanged(int playerId, int place)
    {
        if (playerId == 0 || playerId == 10) return;
        if (place <= 0) return;

        var player = GetOrCreatePlayer(playerId);
        player.PendingLeaderboardPlace = place;

        // Refine confirmed place after PLAYSTATE already fired.
        if (player.IsEliminated)
            player.LeaderboardPlace = place;
    }

    /// <summary>
    /// PLAYSTATE is the real end-of-run signal for the local player. LOST/WON (and CONCEDED)
    /// freeze the board; placement keeps updating from later PLAYER_LEADERBOARD_PLACE tags
    /// until finalize/upload reads the latest value.
    /// </summary>
    public void NotifyPlaystateChanged(int playerId, int playstate)
    {
        if (playerId == 0 || playerId == 10) return;

        const int Won = 4;
        const int Lost = 5;
        const int Conceded = 8;

        if (playstate is not (Won or Lost or Conceded))
            return;

        var player = GetOrCreatePlayer(playerId);
        if (player.IsEliminated) return;

        // Snapshot standing now; later PLAYER_LEADERBOARD_PLACE may refine it.
        var place = player.PendingLeaderboardPlace
            ?? (playstate == Won ? 1 : 0);

        if (place <= 0) return;

        MarkEliminated(playerId, place);
    }

    public void MarkEliminated(int playerId, int place)
    {
        var player = GetOrCreatePlayer(playerId);
        if (player.IsEliminated) return;

        player.LeaderboardPlace = place;
        player.PendingLeaderboardPlace = place;
        RefreshLastKnownBoard(playerId); // freeze board once
        PlayerEliminated?.Invoke(player);
    }
}
