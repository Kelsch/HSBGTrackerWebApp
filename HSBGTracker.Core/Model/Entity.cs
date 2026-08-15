namespace HSBGTracker.Core.Model;

/// <summary>
/// Mirrors a single Hearthstone game entity (minion, hero, hero power, etc).
/// Deliberately a loose tag bag rather than a rigid class hierarchy - that's how
/// the log itself represents entities, and it keeps the parser simple: unknown
/// tags just get ignored instead of breaking anything.
/// </summary>
public sealed class Entity
{
    public int Id { get; }
    public string? CardId { get; set; }
    public string? Name { get; set; }

    private readonly Dictionary<GameTag, int> _tags = new();
    /// <summary>Every numeric tag by exact log name (including names not in <see cref="GameTag"/>).</summary>
    private readonly Dictionary<string, int> _extraTags = new(StringComparer.OrdinalIgnoreCase);

    public Entity(int id) => Id = id;

    public int GetTag(GameTag tag) => _tags.TryGetValue(tag, out var value) ? value : 0;
    public void SetTag(GameTag tag, int value) => _tags[tag] = value;
    public bool HasTag(GameTag tag) => _tags.ContainsKey(tag);

    public int GetExtraTag(string name) =>
        _extraTags.TryGetValue(name, out var value) ? value : 0;

    public void SetExtraTag(string name, int value) => _extraTags[name] = value;

    public IReadOnlyDictionary<GameTag, int> AllTags => _tags;
    public IReadOnlyDictionary<string, int> ExtraTags => _extraTags;

    /// <summary>Typed tag if known, otherwise ExtraTags by name.</summary>
    public int GetTagOrExtra(string name)
    {
        if (Enum.TryParse<GameTag>(name, out var tag) && _tags.TryGetValue(tag, out var typed))
            return typed;
        return GetExtraTag(name);
    }

    public Zone Zone => (Zone)GetTag(GameTag.ZONE);
    public CardType CardType => (CardType)GetTag(GameTag.CARDTYPE);
    public int ControllerPlayerId => GetTag(GameTag.CONTROLLER);
    public int ZonePosition => GetTag(GameTag.ZONE_POSITION);
    public int AttachedToEntityId => GetTag(GameTag.ATTACHED);

    public int Attack => GetTag(GameTag.ATK);
    public int Health => GetTag(GameTag.HEALTH) - GetTag(GameTag.DAMAGE);
    public int TavernTier => GetTag(GameTag.TECH_LEVEL);

    public int ScriptDataNum1 => GetTag(GameTag.TAG_SCRIPT_DATA_NUM_1);
    public int ScriptDataNum2 => GetTag(GameTag.TAG_SCRIPT_DATA_NUM_2);
    public int ScriptDataNum3 => GetTag(GameTag.TAG_SCRIPT_DATA_NUM_3);
    public int ScriptDataNum4 => GetTag(GameTag.TAG_SCRIPT_DATA_NUM_4);

    public bool IsGolden => GetTag(GameTag.PREMIUM) == 1;
    public bool Taunt => GetTag(GameTag.TAUNT) == 1;
    public bool DivineShield => GetTag(GameTag.DIVINE_SHIELD) == 1;
    public bool Poisonous => GetTag(GameTag.POISONOUS) == 1 || GetTag(GameTag.VENOMOUS) == 1;
    public bool Reborn => GetTag(GameTag.REBORN) == 1;
    public bool Windfury => GetTag(GameTag.WINDFURY) == 1 || GetTag(GameTag.MEGA_WINDFURY) == 1;
    public bool Lifesteal => GetTag(GameTag.LIFESTEAL) == 1;
    public bool HasDeathrattle => GetTag(GameTag.DEATHRATTLE) == 1;
    public bool HasBattlecry => GetTag(GameTag.BATTLECRY) == 1;

    public bool IsMinionOnBoard => CardType == CardType.MINION && Zone == Zone.PLAY;
    public bool IsEnchantment => CardType == CardType.ENCHANTMENT;

    public Entity Clone()
    {
        var copy = new Entity(Id) { CardId = CardId, Name = Name };
        foreach (var (tag, value) in _tags)
            copy._tags[tag] = value;
        foreach (var (name, value) in _extraTags)
            copy._extraTags[name] = value;
        return copy;
    }
}
