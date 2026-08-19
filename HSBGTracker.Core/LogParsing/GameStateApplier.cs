using HSBGTracker.Core.Model;

namespace HSBGTracker.Core.LogParsing;

/// <summary>
/// Applies parsed LogPackets to a live GameState. This is the bridge between the generic
/// packet stream and the Battlegrounds-aware GameState/Entity model built earlier.
/// </summary>
public sealed class GameStateApplier
{
    private readonly GameState _state;

    /// <summary>Fires for every TAG_CHANGE processed. Diagnostic-only for now - lets a
    /// consumer watch tag activity live (e.g. filtered to GameEntity) to find the real
    /// game-end signal, since saved logs can be truncated before a long game concludes.</summary>
    public event Action<int, string, string>? TagChanged;

    public GameStateApplier(GameState state) => _state = state;

    public void Apply(LogPacket packet)
    {
        switch (packet)
        {
            case CreateGamePacket:
                _state.Reset();
                break;

            case PlayerNamePacket namePkt:
                _state.RegisterPlayerDisplayName(namePkt.PlayerId, namePkt.Name);
                break;

            case FullEntityPacket full:
                {
                    var entity = _state.GetOrCreateEntity(full.EntityId);
                    if (full.CardId is not null)
                        entity.CardId = full.CardId;

                    foreach (var (tagName, rawValue) in full.Tags)
                    {
                        ApplyTag(entity, tagName, rawValue);
                    }

                    // Resolve CONTROLLER first
                    if (entity.HasTag(GameTag.CONTROLLER))
                    {
                        var rawController = entity.GetTag(GameTag.CONTROLLER);
                        var resolved = _state.TranslateControllerEntityId(rawController) ?? rawController;
                        entity.SetTag(GameTag.CONTROLLER, resolved);
                    }

                    TryUpdatePreCombatOpponentBoard();

                    MaybeCaptureTavernTier(entity);
                    MaybeCaptureHero(entity);
                    MaybeCaptureOpponent(entity);
                    MaybeRefreshBoard(entity);

                    if (entity.AttachedToEntityId != 0
                        && _state.Entities.TryGetValue(entity.AttachedToEntityId, out var host)
                        && host.CardType == CardType.MINION)
                    {
                        _state.RefreshLastKnownBoard(host.ControllerPlayerId);
                    }

                    // Best-effort friendly-player inference: opponent's hand cards arrive
                    // masked (no CardId); yours are fully revealed.
                    if (_state.FriendlyPlayerId is null
                        && entity.Zone == Zone.HAND
                        && entity.CardId is not null
                        && entity.ControllerPlayerId != 0)
                    {
                        _state.FriendlyPlayerId = entity.ControllerPlayerId;
                    }
                    break;
                }

            case ShowEntityPacket show:
                {
                    var id = ResolveId(show.Entity);
                    if (id is null) break;
                    var entity = _state.GetOrCreateEntity(id.Value);
                    if (show.CardId is not null)
                        entity.CardId = show.CardId;

                    foreach (var (tagName, rawValue) in show.Tags)
                    {
                        ApplyTag(entity, tagName, rawValue);
                    }

                    // Resolve CONTROLLER first
                    if (entity.HasTag(GameTag.CONTROLLER))
                    {
                        var rawController = entity.GetTag(GameTag.CONTROLLER);
                        var resolved = _state.TranslateControllerEntityId(rawController) ?? rawController;
                        entity.SetTag(GameTag.CONTROLLER, resolved);
                    }

                    TryUpdatePreCombatOpponentBoard();

                    MaybeCaptureTavernTier(entity);
                    MaybeCaptureHero(entity);
                    MaybeCaptureOpponent(entity);
                    MaybeRefreshBoard(entity);

                    if (entity.AttachedToEntityId != 0
                        && _state.Entities.TryGetValue(entity.AttachedToEntityId, out var host)
                        && host.CardType == CardType.MINION)
                    {
                        _state.RefreshLastKnownBoard(host.ControllerPlayerId);
                    }

                    break;
                }

            case PlayerMappingPacket mapping:
                {
                    _state.RegisterPlayerMapping(mapping.EntityId, mapping.PlayerId);
                    // Player entity also needs CONTROLLER/PLAYER_ID tags so later lookups work.
                    var playerEntity = _state.GetOrCreateEntity(mapping.EntityId);
                    playerEntity.SetTag(GameTag.CONTROLLER, mapping.PlayerId);
                    playerEntity.SetTag(GameTag.CARDTYPE, (int)CardType.PLAYER);
                    break;
                }

            case TagChangePacket tagChange:
                {
                    var id = ResolveId(tagChange.Entity);
                    if (id is null)
                    {
                        // Pairing is often logged as Entity=YourName#1234. If we haven't
                        // mapped that token yet, still apply it to the friendly player.
                        TryApplyUnresolvedPairingTag(tagChange);
                        break;
                    }

                    var entity = _state.GetOrCreateEntity(id.Value);
                    ApplyTag(entity, tagChange.TagName, tagChange.RawValue);

                    // Refresh boards when something important about a minion changes
                    if (IsBoardRelevantTag(tagChange.TagName))
                    {
                        MaybeRefreshBoard(entity);
                    }

                    TryUpdatePreCombatOpponentBoard();

                    // CONTROLLER's raw value is a Player EntityID, not a PlayerID - translate it.
                    if (tagChange.TagName == nameof(GameTag.CONTROLLER)
                        && int.TryParse(tagChange.RawValue, out var controllerEntityId))
                    {
                        var resolvedPlayerId = _state.TranslateControllerEntityId(controllerEntityId)
                            ?? tagChange.Entity.PlayerId;

                        if (resolvedPlayerId is int playerId)
                            entity.SetTag(GameTag.CONTROLLER, playerId);
                    }
                    else if (!entity.HasTag(GameTag.CONTROLLER) && tagChange.Entity.PlayerId is int ownerId)
                    {
                        entity.SetTag(GameTag.CONTROLLER, ownerId);
                    }

                    if (!entity.HasTag(GameTag.CONTROLLER) && tagChange.Entity.PlayerId is int bracketPlayer)
                        entity.SetTag(GameTag.CONTROLLER, bracketPlayer);

                    if (entity.AttachedToEntityId != 0
                        && _state.Entities.TryGetValue(entity.AttachedToEntityId, out var host)
                        && host.CardType == CardType.MINION)
                    {
                        _state.RefreshLastKnownBoard(host.ControllerPlayerId);
                    }

                    TagChanged?.Invoke(id.Value, tagChange.TagName, tagChange.RawValue);

                    var ownerPlayerId = ResolveOwnerPlayerId(entity, tagChange.Entity);

                    if (tagChange.TagName == nameof(GameTag.PLAYER_TECH_LEVEL)
                        && int.TryParse(tagChange.RawValue, out var tavernTier)
                        && ownerPlayerId != 0)
                    {
                        _state.NotifyTavernTierChanged(ownerPlayerId, tavernTier);
                    }

                    if ((tagChange.TagName == nameof(GameTag.NEXT_OPPONENT_PLAYER_ID)
                         || tagChange.TagName == nameof(GameTag.LAST_OPPONENT_PLAYER_ID))
                        && int.TryParse(tagChange.RawValue, out var opponentId)
                        && ownerPlayerId != 0)
                    {
                        _state.NotifyOpponentPaired(ownerPlayerId, opponentId);
                    }

                    if (tagChange.TagName == nameof(GameTag.PLAYER_LEADERBOARD_PLACE)
                        && int.TryParse(tagChange.RawValue, out var place)
                        && ownerPlayerId != 0)
                    {
                        _state.NotifyLeaderboardPlaceChanged(ownerPlayerId, place);
                    }

                    if (tagChange.TagName == nameof(GameTag.PLAYSTATE) && ownerPlayerId != 0)
                    {
                        var playstate = ParsePlaystate(tagChange.RawValue);
                        if (playstate is int ps)
                            _state.NotifyPlaystateChanged(ownerPlayerId, ps);
                    }

                    MaybeCaptureHero(entity);
                    MaybeRefreshBoard(entity);

                    break;
                }

            case BlockStartPacket block:
                if (block.BlockType.Equals("ATTACK", StringComparison.OrdinalIgnoreCase))
                {
                    _state.MarkCombatStarted();   // lock the snapshot
                }
                break;

            case BlockEndPacket:
                break;
        }
    }

    private void TryUpdatePreCombatOpponentBoard()
    {
        if (_state.CurrentOpponentPlayerId is not int oppId) return;
        if (_state.FriendlyPlayerId is not int friendlyId) return;
        if (_state.CombatHasStarted) return;          // already locked

        var opponentMinions = _state.Entities.Values
            .Where(e => e.CardType == CardType.MINION
                     && e.Zone == Zone.PLAY
                     && e.ControllerPlayerId != friendlyId)
            .OrderBy(e => e.ZonePosition)
            .Select(e => e.Clone())
            .ToList();

        if (opponentMinions.Count > 0)
        {
            Console.WriteLine(
    $"[pre-combat] opp={oppId} count={opponentMinions.Count} " +
    $"cards=[{string.Join(", ", opponentMinions.Select(m => m.CardId))}]");

            _state.SetCombatBoard(oppId, opponentMinions);
        }
    }

    private int ResolveOwnerPlayerId(Entity entity, EntityRef entityRef)
    {
        if (entity.ControllerPlayerId != 0)
        {
            return entity.ControllerPlayerId;
        }

        if (entityRef.PlayerId is int bracket)
        {
            return bracket;
        }

        // Player entity itself: CONTROLLER was set from PlayerMappingPacket to PlayerID.
        if (_state.TranslateControllerEntityId(entity.Id) is int mapped)
        {
            return mapped;
        }

        return 0;
    }

    private void MaybeCaptureTavernTier(Entity entity)
    {
        var tier = entity.GetTag(GameTag.PLAYER_TECH_LEVEL);
        if (tier <= 0)
        {
            return;
        }

        var playerId = ResolveOwnerFromEntity(entity);
        if (playerId == 0)
        {
            return;
        }

        _state.NotifyTavernTierChanged(playerId, tier);
    }

    private void MaybeCaptureOpponent(Entity entity)
    {
        var opponentId = entity.GetTag(GameTag.NEXT_OPPONENT_PLAYER_ID);
        if (opponentId == 0)
            opponentId = entity.GetTag(GameTag.LAST_OPPONENT_PLAYER_ID);
        if (opponentId == 0)
            return;

        var playerId = ResolveOwnerFromEntity(entity);
        if (playerId == 0)
            return;

        _state.NotifyOpponentPaired(playerId, opponentId);
    }

    private void MaybeCaptureHero(Entity entity)
    {
        var isHero = entity.CardType == CardType.HERO
            || (entity.CardId is not null
                && entity.CardId.Contains("BaconShop_HERO", StringComparison.OrdinalIgnoreCase));
        if (!isHero || string.IsNullOrEmpty(entity.CardId))
            return;

        var playerId = ResolveOwnerFromEntity(entity);
        if (playerId == 0 || playerId == 10)
            return;

        var player = _state.GetOrCreatePlayer(playerId);
        player.HeroEntityId = entity.Id;

        if (IsBaconPlaceholderHero(entity.CardId)
            && !string.IsNullOrEmpty(player.HeroCardId)
            && !IsBaconPlaceholderHero(player.HeroCardId))
        {
            return;
        }

        player.HeroCardId = entity.CardId;
    }

    //private void MaybeRefreshOpponentBoard(Entity entity)
    //{
    //    if (entity.CardType != CardType.MINION || entity.Zone != Zone.PLAY)
    //        return;

    //    var controller = entity.ControllerPlayerId;
    //    if (controller != 0 && controller == _state.CurrentOpponentPlayerId)
    //        _state.RefreshLastKnownBoard(controller, onlyIfRicher: true);
    //}

    private void MaybeRefreshBoard(Entity entity)
    {
        // Only care about real minions that are (or just were) on a board
        if (entity.CardType != CardType.MINION)
            return;

        var controller = entity.ControllerPlayerId;
        if (controller == 0)
            return;

        // Always keep the friendly board up to date
        if (controller == _state.FriendlyPlayerId)
        {
            _state.RefreshLastKnownBoard(controller);
            return;
        }

        // Keep the current opponent (and the last known opponent) up to date
        if (controller == _state.CurrentOpponentPlayerId
            || controller == _state.LastOpponentPlayerId)
        {
            _state.RefreshLastKnownBoard(controller, onlyIfRicher: true);
        }
    }

    private int ResolveOwnerFromEntity(Entity entity)
    {
        if (entity.ControllerPlayerId != 0)
            return entity.ControllerPlayerId;
        return _state.TranslateControllerEntityId(entity.Id) ?? 0;
    }

    private static bool IsBaconPlaceholderHero(string? cardId) =>
        !string.IsNullOrEmpty(cardId)
        && cardId.Contains("KelThuzad", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves an EntityRef to a numeric entity id. Handles plain ids, bracketed descriptors,
    /// and bare BattleTag / "Bartender Bob" names via the name table populated when we first
    /// see those names paired with known player entity activity.
    /// </summary>
    private int? ResolveId(EntityRef entityRef)
    {
        if (entityRef.Id is int id)
        {
            return id;
        }

        if (entityRef.IsNamedToken == false)
        {
            return null;
        }

        // Already learned?
        if (_state.ResolvePlayerEntityIdByName(entityRef.RawToken) is int known)
        {
            return known;
        }

        var token = entityRef.RawToken;

        if (token.Equals("Bartender Bob", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Bob", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var (entityId, playerId) in GetPlayerMappings())
            {
                if (playerId == 10)
                {
                    _state.RegisterPlayerName(token, entityId);
                    return entityId;
                }
            }
            return null;
        }

        // BattleTag (Name#1234). Do not assign every # token to the friendly player -
        // opponent names also appear this way on PLAYSTATE / leaderboard lines.
        if (token.Contains('#', StringComparison.Ordinal))
        {
            //if (_state.FriendlyPlayerId is int friendlyId)
            //{
            //    var friendly = _state.GetOrCreatePlayer(friendlyId);
            //    if (string.IsNullOrWhiteSpace(friendly.DisplayName))
            //    {
            //        foreach (var (entityId, playerId) in GetPlayerMappings())
            //        {
            //            if (playerId == friendlyId)
            //            {
            //                _state.RegisterPlayerName(token, entityId);
            //                return entityId;
            //            }
            //        }
            //    }
            //}
            int? targetPlayerId = _state.FriendlyPlayerId;
            foreach (var (entityId, playerId) in GetPlayerMappings())
            {
                if (playerId == 10)
                    continue;
                if (targetPlayerId is null || playerId == targetPlayerId)
                {
                    _state.RegisterPlayerName(token, entityId);
                    return entityId;
                }
            }
        }

        return _state.ResolvePlayerEntityIdByName(token);
    }

    private void TryApplyUnresolvedPairingTag(TagChangePacket tagChange)
    {
        if (!IsOpponentPairingTag(tagChange.TagName))
            return;
        if (!int.TryParse(tagChange.RawValue, out var opponentId))
            return;
        if (_state.FriendlyPlayerId is not int friendlyId)
            return;

        Console.WriteLine(
            $"[diagnostic] Unresolved Entity={tagChange.Entity.RawToken} " +
            $"{tagChange.TagName}={opponentId} -> friendly player {friendlyId}");
        _state.NotifyOpponentPaired(friendlyId, opponentId);
    }

    private static bool IsBoardRelevantTag(string tagName) =>
    tagName.Equals(nameof(GameTag.ZONE), StringComparison.OrdinalIgnoreCase)
    || tagName.Equals(nameof(GameTag.ZONE_POSITION), StringComparison.OrdinalIgnoreCase)
    || tagName.Equals(nameof(GameTag.ATK), StringComparison.OrdinalIgnoreCase)
    || tagName.Equals(nameof(GameTag.HEALTH), StringComparison.OrdinalIgnoreCase)
    || tagName.Equals(nameof(GameTag.PREMIUM), StringComparison.OrdinalIgnoreCase)
    || tagName.Equals(nameof(GameTag.TAUNT), StringComparison.OrdinalIgnoreCase)
    || tagName.Equals(nameof(GameTag.DIVINE_SHIELD), StringComparison.OrdinalIgnoreCase)
    || tagName.Equals(nameof(GameTag.CONTROLLER), StringComparison.OrdinalIgnoreCase);

    private static bool IsOpponentPairingTag(string tagName) =>
        tagName.Equals(nameof(GameTag.NEXT_OPPONENT_PLAYER_ID), StringComparison.OrdinalIgnoreCase)
        || tagName.Equals(nameof(GameTag.LAST_OPPONENT_PLAYER_ID), StringComparison.OrdinalIgnoreCase);

    // Expose mappings for ResolveId without making the dictionary public - walk known player entities.
    private IEnumerable<(int EntityId, int PlayerId)> GetPlayerMappings()
    {
        foreach (var entity in _state.Entities.Values)
        {
            if (entity.CardType == CardType.PLAYER && entity.ControllerPlayerId != 0)
            {
                yield return (entity.Id, entity.ControllerPlayerId);
            }
        }
    }

    private static int? ParsePlaystate(string rawValue)
    {
        if (int.TryParse(rawValue, out var n))
        {
            return n;
        }

        // Named values as they appear in Power.log.
        return rawValue.ToUpperInvariant() switch
        {
            "PLAYING" => 1,
            "WINNING" => 2,
            "LOSING" => 3,
            "WON" => 4,
            "LOST" => 5,
            "TIED" => 6,
            "DISCONNECTED" => 7,
            "CONCEDED" => 8,
            _ => null,
        };
    }

    private static void ApplyTag(Entity entity, string tagName, string rawValue)
    {
        if (int.TryParse(rawValue, out var numericValue) == false)
        {
            if (tagName is null)
            {
                return;
            }

            // Named enums for a few non-numeric values
            if (tagName.Equals(nameof(GameTag.ZONE), StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<Zone>(rawValue, out var zone))
            {
                entity.SetTag(GameTag.ZONE, (int)zone);
                entity.SetExtraTag(tagName, (int)zone);
                return;
            }

            if (tagName.Equals(nameof(GameTag.CARDTYPE), StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<CardType>(rawValue, out var cardType))
            {
                entity.SetTag(GameTag.CARDTYPE, (int)cardType);
                entity.SetExtraTag(tagName, (int)cardType);
                return;
            }

            if (tagName.Equals(nameof(GameTag.PLAYSTATE), StringComparison.OrdinalIgnoreCase))
            {
                var ps = ParsePlaystate(rawValue);
                if (ps is int playstate)
                {
                    entity.SetTag(GameTag.PLAYSTATE, playstate);
                    entity.SetExtraTag(tagName, playstate);
                }
            }

            return;
        }

        // Always keep the log name → value (covers brand-new BG tags without a rebuild).
        entity.SetExtraTag(tagName, numericValue);

        if (Enum.TryParse<GameTag>(tagName, out var tag))
        {
            entity.SetTag(tag, numericValue);
        }
    }
}