namespace HSBGTracker.Core.Snapshots;

public enum TrinketTier
{
    Unknown = 0,
    Lesser,
    Greater,
}

public sealed class TrinketSnapshot
{
    public string CardId { get; set; } = "";
    public string Name { get; set; } = "";
    public int EntityId { get; set; }
    public TrinketTier Tier { get; set; }
    public int ScriptDataNum1 { get; set; }
    public int ScriptDataNum2 { get; set; }
    public int ScriptDataNum3 { get; set; }
    public int ScriptDataNum4 { get; set; }
    public List<AttachedEntitySnapshot> Attachments { get; set; } = new();
    public Dictionary<string, int> Tags { get; set; } = new();
}
