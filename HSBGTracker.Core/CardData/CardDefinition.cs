using HSBGTracker.Core.Snapshots;

namespace HSBGTracker.Core.CardData;

/// <summary>
/// Static reference data for one card, sourced from an external dataset (HearthstoneJSON's
/// battlegrounds card list is the standard free source) rather than the live log. This is
/// deliberately narrow - only the fields the scorer actually needs, not a full card model.
/// </summary>
public sealed class CardDefinition
{
    public string CardId { get; set; } = "";
    public string Name { get; set; } = "";
    public int BaseAttack { get; set; }
    public int BaseHealth { get; set; }
    public int TechLevel { get; set; }

    /// <summary>Set only for Trinket cards; null for everything else.</summary>
    public TrinketTier? TrinketTier { get; set; }

    /// <summary>
    /// What this card's Deathrattle (or equivalent summon-on-death effect) produces.
    /// Empty for cards with no such effect. This is the piece the live log can never
    /// tell you - it only exists in card text.
    /// </summary>
    public List<SummonDefinition> Summons { get; set; } = new();
}

public sealed class SummonDefinition
{
    public string CardId { get; set; } = "";
    public int Count { get; set; } = 1;

    /// <summary>How many times the Deathrattle fires before it's used up - e.g. golden
    /// versions of a minion generally trigger their Deathrattle twice.</summary>
    public int Triggers { get; set; } = 1;
}
