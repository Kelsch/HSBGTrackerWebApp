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
            if (value is int friendlyId)
            {
                var player = GetOrCreatePlayer(friendlyId);
                if (player.LastOpponentPlayerId is int opp)
                {
                    LastOpponentPlayerId = opp;
                    CurrentOpponentPlayerId = player.CurrentOpponentPlayerId;
                }
                FriendlyPlayerIdentified?.Invoke(friendlyId);
            }
        }
    }

    /// <summary>Friendly player's current/next combat opponent PlayerID.</summary>
    public int? CurrentOpponentPlayerId { get; private set; }

    /// <summary>Friendly player's most recent real combat opponent PlayerID.</summary>
    public int? LastOpponentPlayerId { get; private set; }

    public event Action<int>? FriendlyPlayerIdentified;
    public event Action<PlayerState>? PlayerEliminated;
    /// <summary>Fires when the friendly player is paired for combat (playerId, opponentPlayerId).</summary>
    public event Action<int, int>? OpponentPaired;

    private readonly Dictionary<int, List<Entity>> _lastKnownBoards = new();
    private readonly Dictionary<int, List<Entity>> _lastKnownAttachments = new();

    private int? _combatSnapshotTakenForOpponent;
    private int? _combatSnapshotOpponent;
    private bool _combatHasStarted;

    public bool CombatHasStarted => _combatHasStarted;

    public bool IsCombatActive { get; private set; }

    public void MarkCombatStarted() => IsCombatActive = true;
    public void MarkCombatEnded() => IsCombatActive = false;

    public void MarkCombatSnapshotTaken(int opponentPlayerId)
    {
        _combatSnapshotTakenForOpponent = opponentPlayerId;
    }

    public bool HasTakenCombatSnapshot(int opponentPlayerId) =>
        _combatSnapshotTakenForOpponent == opponentPlayerId;

    /// <summary>Call after any tag mutation that could affect board state. Cheap no-op unless
    /// the player currently has minions in PLAY. Clones the live board so the snapshot survives
    /// later combat/end-of-game cleanup that mutates the same entities out of PLAY.</summary>
    public void RefreshLastKnownBoard(int playerId, bool onlyIfRicher = false)
    {
        if (playerId == 0) return;

        var board = GetBoard(playerId);
        if (board.Count == 0) return;

        if (onlyIfRicher
            && _lastKnownBoards.TryGetValue(playerId, out var existing)
            && existing.Count > board.Count)
        {
            return;
        }

        _lastKnownBoards[playerId] = board.Select(e => e.Clone()).ToList();

        var boardIds = board.Select(e => e.Id).ToHashSet();
        // Enchantments / Dark Gifts / buffs attached to those minions (any zone - end-of-game moves them).
        _lastKnownAttachments[playerId] = Entities.Values
            .Where(e => e.AttachedToEntityId != 0 && boardIds.Contains(e.AttachedToEntityId))
            .Select(e => e.Clone())
            .ToList();
    }

    private readonly Dictionary<int, List<Entity>> _lastKnownTrinkets = new();

    public void RefreshLastKnownTrinkets(int playerId)
    {
        if (playerId == 0) return;

        var trinkets = Entities.Values
            .Where(e => e.ControllerPlayerId == playerId && e.CardType == CardType.TRINKET && e.Zone == Zone.PLAY)
            .ToList();

        if (trinkets.Count == 0) return;

        _lastKnownTrinkets[playerId] = trinkets.Select(e => e.Clone()).ToList();
    }

    public IReadOnlyList<Entity> GetFinalTrinkets(int playerId) => _lastKnownTrinkets.TryGetValue(playerId, out var trinkets) ? trinkets : Array.Empty<Entity>();

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
        if (TranslateControllerEntityId(entityId) is int playerId)
            ApplyDisplayName(playerId, name);
    }

    public void RegisterPlayerDisplayName(int playerId, string name)
    {
        if (playerId == 0 || playerId == 10) return;
        if (!ApplyDisplayName(playerId, name)) return;

        if (TranslatePlayerIdToEntityId(playerId) is int entityId)
            _nameToEntityId[name] = entityId;
    }

    private bool ApplyDisplayName(int playerId, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.Equals("UNKNOWN HUMAN PLAYER", StringComparison.OrdinalIgnoreCase)) return false;
        if (name.Equals("Bartender Bob", StringComparison.OrdinalIgnoreCase)) return false;
        if (name.Equals("Bob", StringComparison.OrdinalIgnoreCase)) return false;

        var player = GetOrCreatePlayer(playerId);
        // Prefer a BattleTag over a hero title if we already have one.
        if (!string.IsNullOrWhiteSpace(player.DisplayName) && player.DisplayName.Contains('#') && !name.Contains('#'))
            return false;

        player.DisplayName = name;
        return true;
    }

    public int? TranslateControllerEntityId(int entityId) =>
        _entityIdToPlayerId.TryGetValue(entityId, out var playerId) ? playerId : null;

    public int? TranslatePlayerIdToEntityId(int playerId)
    {
        foreach (var (entityId, mapped) in _entityIdToPlayerId)
        {
            if (mapped == playerId)
                return entityId;
        }
        return null;
    }

    /// <summary>If the log used a player EntityID where a PlayerID is expected, translate it.</summary>
    public int NormalizePlayerId(int id)
    {
        if (id == 0) return 0;
        return TranslateControllerEntityId(id) ?? id;
    }

    /// <summary>Resolve a bare log token (BattleTag, "GameEntity", numeric id already handled
    /// by EntityRef) to a player EntityID.</summary>
    public int? ResolvePlayerEntityIdByName(string name) =>
        _nameToEntityId.TryGetValue(name, out var id) ? id : null;

    public string? ResolvePlayerName(int playerId)
    {
        if (playerId == 0) return null;

        var player = GetOrCreatePlayer(playerId);
        if (!string.IsNullOrWhiteSpace(player.DisplayName))
            return player.DisplayName;

        foreach (var (name, entityId) in _nameToEntityId)
        {
            if (TranslateControllerEntityId(entityId) == playerId)
                return name;
        }

        return player.HeroCardId;
    }

    public void Reset()
    {
        Entities.Clear();
        Players.Clear();
        GameId = null;
        FriendlyPlayerId = null;
        CurrentOpponentPlayerId = null;
        LastOpponentPlayerId = null;
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

    public Entity? GetHero(int playerId) =>
        Entities.Values.FirstOrDefault(e =>
            e.ControllerPlayerId == playerId
            && e.CardType == CardType.HERO
            && !string.IsNullOrEmpty(e.CardId));

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
    /// NEXT_OPPONENT_PLAYER_ID / LAST_OPPONENT_PLAYER_ID. Every lobby player gets this each
    /// combat; only the friendly player's pairing is promoted to Current/LastOpponentPlayerId.
    /// </summary>
    public void NotifyOpponentPaired(int playerId, int opponentRawId)
    {
        if (playerId == 0 || playerId == 10) return;

        var opponentPlayerId = NormalizePlayerId(opponentRawId);
        var player = GetOrCreatePlayer(playerId);

        if (opponentPlayerId <= 0 || opponentPlayerId == 10 || opponentPlayerId == playerId)
        {
            player.CurrentOpponentPlayerId = null;
            if (playerId == FriendlyPlayerId)
                CurrentOpponentPlayerId = null;
            return;
        }

        player.CurrentOpponentPlayerId = opponentPlayerId;
        player.LastOpponentPlayerId = opponentPlayerId;

        // Capture both boards, but do NOT wipe a previously good cache
        RefreshLastKnownBoard(playerId);
        RefreshLastKnownBoard(opponentPlayerId);   // will only overwrite if it finds minions

        // Explicitly remember hero if we already know it
        var opponent = GetOrCreatePlayer(opponentPlayerId);
        if (string.IsNullOrEmpty(opponent.HeroCardId))
        {
            var hero = GetHero(opponentPlayerId);
            if (hero?.CardId is not null)
                opponent.HeroCardId = hero.CardId;
        }

        //var trinkets = GetTrinkets(opponentPlayerId);
        var trinkets = GetFinalTrinkets(opponentPlayerId);
        if (trinkets.Count > 0)
        {
            // store them somewhere if you want (you may need a _lastKnownTrinkets dictionary)
        }

        var liveCount = GetBoard(opponentPlayerId).Count;
        var cachedCount = _lastKnownBoards.TryGetValue(opponentPlayerId, out var cached) ? cached.Count : 0;

        Console.WriteLine(
            $"[board-debug] Pairing {playerId} vs {opponentPlayerId} | " +
            $"live board={liveCount} | cached board={cachedCount}");

        if (playerId == FriendlyPlayerId)
        {
            CurrentOpponentPlayerId = opponentPlayerId;
            LastOpponentPlayerId = opponentPlayerId;

            //_combatSnapshotTakenForOpponent = null;
            _combatSnapshotOpponent = null;
            _combatHasStarted = false;

            OpponentPaired?.Invoke(playerId, opponentPlayerId);
        }
    }

    public void SetCombatBoard(int opponentPlayerId, List<Entity> board)
    {
        if (board.Count == 0) return;

        if (_lastKnownBoards.TryGetValue(opponentPlayerId, out var existing))
        {
            // Keep the larger board – the real pre-combat board is almost always bigger
            // than the later token-only versions.
            if (existing.Count >= board.Count)
                return;

            // Optional extra filter – reject boards that are almost pure tokens
            //var uniqueCards = board.Select(m => m.CardId).Distinct().Count();
            //if (uniqueCards <= 1 && board.Count > 3)
            //    return; // probably just a bunch of the same beetle
        }

        _lastKnownBoards[opponentPlayerId] = board;
        _combatSnapshotOpponent = opponentPlayerId;

        Console.WriteLine($"[combat-debug] Updated opponent {opponentPlayerId} board → {board.Count} minions");
    }

    public void SetLastKnownBoard(int playerId, List<Entity> board)
    {
        if (playerId == 0 || board.Count == 0) return;

        _lastKnownBoards[playerId] = board;

        var boardIds = board.Select(e => e.Id).ToHashSet();
        _lastKnownAttachments[playerId] = Entities.Values
            .Where(e => e.AttachedToEntityId != 0 && boardIds.Contains(e.AttachedToEntityId))
            .Select(e => e.Clone())
            .ToList();
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
        Console.WriteLine($"[diag] NotifyPlaystateChanged playerId={playerId} playstate={playstate}");

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

        Console.WriteLine($"[diag] -> eliminating playerId={playerId} place={place} " + $"(pending was {player.PendingLeaderboardPlace?.ToString() ?? "null"})");

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
