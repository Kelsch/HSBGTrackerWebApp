namespace HSBGTracker.Core.Model;

/// <summary>
/// Subset of Hearthstone's entity tags relevant to Battlegrounds board tracking.
/// Names match the tag names as they appear in Power.log (TAG_CHANGE / FULL_ENTITY lines),
/// so the log parser can Enum.TryParse directly against these.
/// Extend as needed once you see real log output - there are ~300 known tags total,
/// this only covers what a board/hand-strength tracker needs.
/// </summary>
public enum GameTag
{
    UNKNOWN = 0,

    // Structural
    ZONE,
    ZONE_POSITION,
    CONTROLLER,
    ENTITY_ID,
    CARDTYPE,
    CARDRACE,
    RARITY,
    CREATOR,
    CREATOR_DBID,
    /// <summary>Entity id of the host minion/trinket this enchantment is attached to.</summary>
    ATTACHED,
    PARENT_CARD,
    LINKED_ENTITY,
    PLAYER_ID,
    HERO_ENTITY,

    // Combat stats
    ATK,
    HEALTH,
    DAMAGE,
    ARMOR,
    MAXHEALTHATSTARTOFGAME,

    // Battlegrounds specific
    TECH_LEVEL,
    PLAYER_TECH_LEVEL,
    PLAYER_LEADERBOARD_PLACE,
    PREMIUM,
    BACON_IS_KELTHUZAD_DUPLICATE,
    /// <summary>PlayerID of the next combat opponent. Set on the player/hero each round.</summary>
    NEXT_OPPONENT_PLAYER_ID,
    /// <summary>PlayerID of the most recent combat opponent. Backup if NEXT is cleared.</summary>
    LAST_OPPONENT_PLAYER_ID,
    /// <summary>Often marks BG spellcraft / useable tavern spells; confirm in your logs.</summary>
    BACON_IS_BOB_QUEST,
    /// <summary>Dark Gift / anomaly style markers appear under various BACON_* names - ExtraTags catches unknowns.</summary>
    BACON_DARK_GIFT,

    // Keywords
    TAUNT,
    DIVINE_SHIELD,
    POISONOUS,
    VENOMOUS,
    REBORN,
    WINDFURY,
    MEGA_WINDFURY,
    STEALTH,
    LIFESTEAL,
    RUSH,
    CHARGE,
    DEATHRATTLE,
    BATTLECRY,
    START_OF_COMBAT,
    FRENZY,
    AURA,
    TRIGGER_VISUAL,

    // Scaled effect magnitudes (deathrattle damage, summon counts, buff sizes, etc.)
    TAG_SCRIPT_DATA_NUM_1,
    TAG_SCRIPT_DATA_NUM_2,
    TAG_SCRIPT_DATA_NUM_3,
    TAG_SCRIPT_DATA_NUM_4,
    TAG_SCRIPT_DATA_NUM_5,
    TAG_SCRIPT_DATA_NUM_6,

    // Activatable / use-power style state (presence varies by patch - ExtraTags is fallback)
    CUSTOM_KEYWORD_EFFECT,
    USE_ALTERNATE_CARD_TEXT,
    BACON_ACTION_CARD,
    HERO_POWER_ENTITY,
    ADDITIONAL_HERO_POWER_ENTITY_1,

    // Turn/meta
    NUM_TURNS_IN_PLAY,
    NUM_ATTACKS_THIS_TURN,
    PLAYSTATE,
}
