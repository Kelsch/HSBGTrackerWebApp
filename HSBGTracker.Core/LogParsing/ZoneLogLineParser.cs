using System.Text.RegularExpressions;

namespace HSBGTracker.Core.LogParsing;

/// <summary>
/// Parses Zone.log's ZoneChangeList.ProcessChanges() lines - each one tags an entity with
/// local=True/False and player=N, which is a far more reliable "who am I" signal than the
/// hand-reveal heuristic in GameStateApplier (that trick assumes constructed-Hearthstone hand
/// hiding, which doesn't apply the same way in Battlegrounds). NOT YET CONFIRMED against a
/// real captured Zone.log - adjust the regex once you have one to check against.
/// </summary>
public sealed class ZoneLogLineParser
{
    private static readonly Regex ProcessChangesRegex = new(
        @"ZoneChangeList\.ProcessChanges\(\)\s*-\s*id=\d+\s+local=(?<local>True|False)\s*\[.*?player=(?<player>\d+)",
        RegexOptions.Compiled);

    /// <summary>Returns the player id the first time a local=True line is seen, else null.</summary>
    public int? TryGetFriendlyPlayerId(string rawLine)
    {
        var match = ProcessChangesRegex.Match(rawLine);
        if (!match.Success) return null;
        if (match.Groups["local"].Value != "True") return null;
        return int.Parse(match.Groups["player"].Value);
    }
}

