namespace HSBGTracker.Core.Snapshots;

/// <summary>
/// A frozen, storage-friendly copy of one minion at snapshot time. Deliberately flat
/// (no references back into GameState) so it can be JSON-serialized into a DB column
/// and re-hydrated later without carrying the whole live game state with it.
/// </summary>
public sealed class MinionSnapshot
{
    public string CardId { get; set; } = "";
    public string Name { get; set; } = "";
    public int EntityId { get; set; }
    public int Attack { get; set; }
    public int Health { get; set; }
    public int TavernTier { get; set; }
    public int BoardPosition { get; set; }
    public bool IsGolden { get; set; }

    public bool Taunt { get; set; }
    public bool DivineShield { get; set; }
    public bool Poisonous { get; set; }
    public bool Reborn { get; set; }
    public bool Windfury { get; set; }
    public bool Lifesteal { get; set; }
    public bool HasDeathrattle { get; set; }
    public bool HasBattlecry { get; set; }

    /// <summary>Scaled effect values from the log (DR damage, summon count, buff size, etc.).</summary>
    public int ScriptDataNum1 { get; set; }
    public int ScriptDataNum2 { get; set; }
    public int ScriptDataNum3 { get; set; }
    public int ScriptDataNum4 { get; set; }

    /// <summary>Dark Gifts, enchantments, temporary effects attached to this minion.</summary>
    public List<AttachedEntitySnapshot> Attachments { get; set; } = new();

    /// <summary>All numeric tags by Power.log name - display/API can pick new mechanics without a client update.</summary>
    public Dictionary<string, int> Tags { get; set; } = new();
}
