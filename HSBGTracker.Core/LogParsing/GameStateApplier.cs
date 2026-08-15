using HSBGTracker.Core.Model;
using System.Numerics;

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

            case FullEntityPacket full:
                {
                    var entity = _state.GetOrCreateEntity(full.EntityId);
                    if (full.CardId is not null)
                        entity.CardId = full.CardId;

                    foreach (var (tagName, rawValue) in full.Tags)
                        ApplyTag(entity, tagName, rawValue);

                    if (entity.AttachedToEntityId != 0
                        && _state.Entities.TryGetValue(entity.AttachedToEntityId, out var host)
                        && host.CardType == CardType.MINION)
                    {
                        _state.RefreshLastKnownBoard(host.ControllerPlayerId);
                    }

                    //if (entity.CardType == CardType.MINION)
                    //    _state.RefreshLastKnownBoard(entity.ControllerPlayerId);

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
                        ApplyTag(entity, tagName, rawValue);

                    if (entity.AttachedToEntityId != 0
                        && _state.Entities.TryGetValue(entity.AttachedToEntityId, out var host)
                        && host.CardType == CardType.MINION)
                    {
                        _state.RefreshLastKnownBoard(host.ControllerPlayerId);
                    }

                    //if (entity.CardType == CardType.MINION)
                    //    _state.RefreshLastKnownBoard(entity.ControllerPlayerId);

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
                        // First time we see a bare BattleTag, learn the mapping if this tag
                        // change itself is on a known player entity id... we can't. Instead
                        // learn names when we see Entity=Name attached to known player entity
                        // via other paths. For named tokens we still try name table below.
                        break;
                    }

                    var entity = _state.GetOrCreateEntity(id.Value);
                    ApplyTag(entity, tagChange.TagName, tagChange.RawValue);

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

                    //if (entity.CardType == CardType.MINION)
                    //    _state.RefreshLastKnownBoard(entity.ControllerPlayerId);

                    TagChanged?.Invoke(id.Value, tagChange.TagName, tagChange.RawValue);

                    var ownerPlayerId = ResolveOwnerPlayerId(entity, tagChange.Entity);

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

                    break;
                }

            case BlockStartPacket:
            case BlockEndPacket:
                break;
        }
    }

    private int ResolveOwnerPlayerId(Entity entity, EntityRef entityRef)
    {
        if (entity.ControllerPlayerId != 0)
            return entity.ControllerPlayerId;

        if (entityRef.PlayerId is int bracket)
            return bracket;

        // Player entity itself: CONTROLLER was set from PlayerMappingPacket to PlayerID.
        if (_state.TranslateControllerEntityId(entity.Id) is int mapped)
            return mapped;

        return 0;
    }

    /// <summary>
    /// Resolves an EntityRef to a numeric entity id. Handles plain ids, bracketed descriptors,
    /// and bare BattleTag / "Bartender Bob" names via the name table populated when we first
    /// see those names paired with known player entity activity.
    /// </summary>
    private int? ResolveId(EntityRef entityRef)
    {
        if (entityRef.Id is int id)
            return id;

        if (!entityRef.IsNamedToken)
            return null;

        // Already learned?
        if (_state.ResolvePlayerEntityIdByName(entityRef.RawToken) is int known)
            return known;

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

        // BattleTag (Name#1234) - local logs only have one real human player entity.
        if (token.Contains('#', StringComparison.Ordinal))
        {
            // Prefer known friendly player; otherwise the only non-Bob PLAYER entity.
            int? targetPlayerId = _state.FriendlyPlayerId;
            foreach (var (entityId, playerId) in GetPlayerMappings())
            {
                if (playerId == 10) continue;
                if (targetPlayerId is null || playerId == targetPlayerId)
                {
                    _state.RegisterPlayerName(token, entityId);
                    return entityId;
                }
            }
        }

        return _state.ResolvePlayerEntityIdByName(token);
    }

    // Expose mappings for ResolveId without making the dictionary public - walk known player entities.
    private IEnumerable<(int EntityId, int PlayerId)> GetPlayerMappings()
    {
        foreach (var entity in _state.Entities.Values)
        {
            if (entity.CardType == CardType.PLAYER && entity.ControllerPlayerId != 0)
                yield return (entity.Id, entity.ControllerPlayerId);
        }
    }

    private static int? ParsePlaystate(string rawValue)
    {
        if (int.TryParse(rawValue, out var n))
            return n;

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
        if (!int.TryParse(rawValue, out var numericValue))
        {
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
            entity.SetTag(tag, numericValue);
    }
}