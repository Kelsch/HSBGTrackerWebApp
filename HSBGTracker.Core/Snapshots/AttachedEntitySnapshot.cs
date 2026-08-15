using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSBGTracker.Core.Snapshots;

/// <summary>
/// Enchantment, Dark Gift, or other entity attached to a minion/trinket via ATTACHED.
/// CardId + ScriptData + Tags are what the display app needs to render effect text/power.
/// </summary>
public sealed class AttachedEntitySnapshot
{
    public int EntityId { get; set; }
    public string CardId { get; set; } = "";
    public string Name { get; set; } = "";
    public string CardType { get; set; } = "";
    public int ScriptDataNum1 { get; set; }
    public int ScriptDataNum2 { get; set; }
    public int ScriptDataNum3 { get; set; }
    public int ScriptDataNum4 { get; set; }
    /// <summary>Full tag bag by log name for forward-compatible UI.</summary>
    public Dictionary<string, int> Tags { get; set; } = new();
}
