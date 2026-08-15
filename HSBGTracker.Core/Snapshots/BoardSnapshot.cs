namespace HSBGTracker.Core.Snapshots;

public sealed class BoardSnapshot
{
    public int PlayerId { get; set; }
    public string? HeroCardId { get; set; }
    public HeroPowerSnapshot? HeroPower { get; set; }
    public int TavernTier { get; set; }
    public List<MinionSnapshot> Minions { get; set; } = new();
    public List<TrinketSnapshot> Trinkets { get; set; } = new();

    /// <summary>
    /// Placeholder for your scoring algorithm - left null until you run a scorer over
    /// this snapshot. Kept separate from the raw data so you can re-score historical
    /// games later without re-parsing logs, once you improve the scoring logic.
    /// </summary>
    public double? Score { get; set; }
}
