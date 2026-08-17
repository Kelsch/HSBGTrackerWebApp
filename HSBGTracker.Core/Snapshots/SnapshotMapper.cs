using HSBGTracker.Core.CardData;
using HSBGTracker.Core.Model;

namespace HSBGTracker.Core.Snapshots;

/// <summary>
/// Converts live GameState/Entity objects into frozen BoardSnapshot records. Call this
/// as your "pre-combat" trigger fires, once for the friendly player and once for whichever
/// opponent they're currently paired against - see the note on trigger timing: the
/// pre-combat moment is the accurate source for what a board looked like, while a
/// PlayerEliminated-style event is only the signal for when to persist the most recent
/// snapshot you already took.
/// </summary>
public static class SnapshotMapper
{
    /// <param name="cardDb">Optional - if supplied, resolves Trinket Lesser/Greater tier,
    /// which isn't readable from live entity tags.</param>
    public static BoardSnapshot ToBoardSnapshot(GameState state, int playerId, ICardDatabase? cardDb = null)
    {
        var player = state.GetOrCreatePlayer(playerId);
        var board = state.GetFinalBoard(playerId);
        var attachments = state.GetFinalAttachments(playerId);
        var byHost = attachments.ToLookup(a => a.AttachedToEntityId);
        var trinkets = state.GetTrinkets(playerId);
        var heroPower = state.GetHeroPower(playerId);
        var hero = state.GetHero(playerId);

        return new BoardSnapshot
        {
            PlayerId = playerId,
            HeroCardId = player.HeroCardId ?? hero?.CardId,
            HeroPower = heroPower is null ? null : ToHeroPowerSnapshot(heroPower),
            TavernTier = state.ResolveTavernTier(playerId),
            Minions = board.Select(m => ToMinionSnapshot(m, byHost[m.Id])).ToList(),
            Trinkets = trinkets.Select(t => ToTrinketSnapshot(t, byHost[t.Id], cardDb)).ToList(),
        };
    }

    private static MinionSnapshot ToMinionSnapshot(Entity e, IEnumerable<Entity> attachments) => new()
    {
        CardId = e.CardId ?? "",
        Name = e.Name ?? "",
        EntityId = e.Id,
        Attack = e.Attack,
        Health = e.Health,
        TavernTier = e.TavernTier,
        BoardPosition = e.ZonePosition,
        IsGolden = e.IsGolden,
        Taunt = e.Taunt,
        DivineShield = e.DivineShield,
        Poisonous = e.Poisonous,
        Reborn = e.Reborn,
        Windfury = e.Windfury,
        Lifesteal = e.Lifesteal,
        HasDeathrattle = e.HasDeathrattle,
        HasBattlecry = e.HasBattlecry,
        ScriptDataNum1 = e.ScriptDataNum1,
        ScriptDataNum2 = e.ScriptDataNum2,
        ScriptDataNum3 = e.ScriptDataNum3,
        ScriptDataNum4 = e.ScriptDataNum4,
        Attachments = attachments.Select(ToAttachedSnapshot).ToList(),
        Tags = CopyTags(e),
    };

    private static HeroPowerSnapshot ToHeroPowerSnapshot(Entity e) => new()
    {
        CardId = e.CardId ?? "",
        Name = e.Name ?? "",
    };

    private static TrinketSnapshot ToTrinketSnapshot(Entity e, IEnumerable<Entity> attachments, ICardDatabase? cardDb)
    {
        var def = cardDb?.Find(e.CardId ?? "");
        return new TrinketSnapshot
        {
            CardId = e.CardId ?? "",
            Name = e.Name ?? "",
            EntityId = e.Id,
            Tier = def?.TrinketTier ?? TrinketTier.Unknown,
            ScriptDataNum1 = e.ScriptDataNum1,
            ScriptDataNum2 = e.ScriptDataNum2,
            ScriptDataNum3 = e.ScriptDataNum3,
            ScriptDataNum4 = e.ScriptDataNum4,
            Attachments = attachments.Select(ToAttachedSnapshot).ToList(),
            Tags = CopyTags(e),
        };
    }

    private static AttachedEntitySnapshot ToAttachedSnapshot(Entity e) => new()
    {
        EntityId = e.Id,
        CardId = e.CardId ?? "",
        Name = e.Name ?? "",
        CardType = e.CardType.ToString(),
        ScriptDataNum1 = e.ScriptDataNum1,
        ScriptDataNum2 = e.ScriptDataNum2,
        ScriptDataNum3 = e.ScriptDataNum3,
        ScriptDataNum4 = e.ScriptDataNum4,
        Tags = CopyTags(e),
    };

    private static Dictionary<string, int> CopyTags(Entity e)
    {
        // Prefer ExtraTags (complete name→value). Overlay typed enum names for convenience.
        var dict = new Dictionary<string, int>(e.ExtraTags, StringComparer.OrdinalIgnoreCase);
        foreach (var (tag, value) in e.AllTags)
            dict[tag.ToString()] = value;
        return dict;
    }
}