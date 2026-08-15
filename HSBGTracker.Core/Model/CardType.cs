namespace HSBGTracker.Core.Model;

public enum CardType
{
    INVALID = 0,
    GAME = 1,
    /// <summary>Player game-object (not a card). Logged as CARDTYPE=PLAYER on
    /// "Player EntityID=X PlayerID=Y" entities - used so we can find those entities later
    /// when resolving BattleTag names like "DalTron#11868".</summary>
    PLAYER = 2,
    HERO = 3,
    MINION = 4,
    SPELL = 5,
    ENCHANTMENT = 6,
    WEAPON = 7,
    HERO_POWER = 10,

    /// <summary>
    /// Battlegrounds trinkets print as CARDTYPE=BATTLEGROUND_TRINKET in Power.log.
    /// Numeric id isn't critical for us as long as Enum.TryParse hits this name.
    /// </summary>
    BATTLEGROUND_TRINKET = 44,

    /// <summary>Alias used by GetTrinkets filters.</summary>
    TRINKET = BATTLEGROUND_TRINKET,
}
