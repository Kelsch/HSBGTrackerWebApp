using System.Text.RegularExpressions;

namespace HSBGTracker.Core.LogParsing;

/// <summary>
/// Parses individual Power.log lines into LogPacket objects. Stateful across calls: a
/// FULL_ENTITY or SHOW_ENTITY header line is followed by zero or more "tag=X value=Y"
/// lines at deeper indentation, which enrich that same packet rather than becoming their
/// own packets. The pending packet is only "closed" (returned to the caller) once a line
/// arrives at indentation less-or-equal to the header's own - so ParseLine can return more
/// than one packet for a single input line (the just-closed pending packet, plus whatever
/// the new line itself represents).
/// </summary>
public sealed class PowerLogLineParser
{
    // e.g. "D 09:01:05.7959635 GameState.DebugPrintPower() -     TAG_CHANGE Entity=... "
    // Captures indentation (used for block/tag nesting) and the content after the dash.
    private static readonly Regex PrefixRegex = new(
        @"^\S+ [\d:.]+ \S+\.DebugPrintPower\(\) - (?<indent>\s*)(?<rest>.*)$",
        RegexOptions.Compiled);

    // e.g. "D 09:01:05.7959635 GameState.DebugPrintGame() - PlayerID=2, PlayerName=Foo#1234"
    private static readonly Regex DebugPrintGamePlayerRegex = new(
        @"^\S+ [\d:.]+\s+\S+\.DebugPrintGame\(\) - PlayerID=(?<playerId>\d+),\s*PlayerName=(?<name>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex TagChangeRegex = new(
        @"^TAG_CHANGE Entity=(?<entity>.+?) tag=(?<tag>\S+) value=(?<value>\S+)",
        RegexOptions.Compiled);

    private static readonly Regex FullEntityCreateRegex = new(
        @"^FULL_ENTITY - Creating ID=(?<id>\d+)(?: CardID=(?<cardId>\S*))?",
        RegexOptions.Compiled);

    /// <summary>Alternate FULL_ENTITY shape - "Updating [bracketed descriptor] CardID=..." -
    /// used when Hearthstone resends full info for an entity that already exists (e.g. shop
    /// reroll buttons, and confirmed against a real log to also cover ordinary minion creation
    /// in some cases). Previously unhandled entirely, silently dropping CardId/CARDTYPE for any
    /// entity whose first appearance came through this form - see conversation history.</summary>
    private static readonly Regex FullEntityUpdateRegex = new(
        @"^FULL_ENTITY - Updating (?<entity>\[.+?\]) CardID=(?<cardId>\S*)",
        RegexOptions.Compiled);

    private static readonly Regex ShowEntityRegex = new(
        @"^SHOW_ENTITY - Updating Entity=(?<entity>.+?) CardID=(?<cardId>\S*)",
        RegexOptions.Compiled);

    private static readonly Regex TrailingTagRegex = new(
        @"^tag=(?<tag>\S+) value=(?<value>\S+)",
        RegexOptions.Compiled);

    private static readonly Regex BlockStartRegex = new(
        @"^BLOCK_START BlockType=(?<blockType>\S+)(?: Entity=(?<entity>.+?))?(?: .*)?$",
        RegexOptions.Compiled);

    private static readonly Regex PlayerMappingRegex = new(
    @"^Player EntityID=(?<entityId>\d+) PlayerID=(?<playerId>\d+)",
    RegexOptions.Compiled);

    private LogPacket? _pendingHeader;
    private int _pendingIndent;

    public IReadOnlyList<LogPacket> ParseLine(string rawLine)
    {
        if (DebugPrintGamePlayerRegex.Match(rawLine) is { Success: true } gamePlayer)
        {
            return new LogPacket[]
            {
                new PlayerNamePacket(
                    int.Parse(gamePlayer.Groups["playerId"].Value),
                    gamePlayer.Groups["name"].Value.Trim()),
            };
        }

        var prefixMatch = PrefixRegex.Match(rawLine);
        if (!prefixMatch.Success)
            return Array.Empty<LogPacket>(); // not a DebugPrintPower line - chat, other loggers, etc.

        var indent = prefixMatch.Groups["indent"].Value.Length;
        var content = prefixMatch.Groups["rest"].Value;

        // Continuation of a pending FULL_ENTITY/SHOW_ENTITY block?
        if (_pendingHeader is not null && indent > _pendingIndent)
        {
            var tagMatch = TrailingTagRegex.Match(content);
            if (tagMatch.Success)
            {
                var tagName = tagMatch.Groups["tag"].Value;
                var value = tagMatch.Groups["value"].Value;
                switch (_pendingHeader)
                {
                    case FullEntityPacket fe: fe.Tags.Add((tagName, value)); break;
                    case ShowEntityPacket se: se.Tags.Add((tagName, value)); break;
                }
                return Array.Empty<LogPacket>(); // enriches the pending packet, no new packet yet
            }
        }

        var results = new List<LogPacket>();
        var flushed = FlushPending();
        if (flushed is not null) results.Add(flushed);

        if (TagChangeRegex.Match(content) is { Success: true } tagChange)
        {
            results.Add(new TagChangePacket(
                EntityRef.Parse(tagChange.Groups["entity"].Value),
                tagChange.Groups["tag"].Value,
                tagChange.Groups["value"].Value));
            return results;
        }

        if (FullEntityCreateRegex.Match(content) is { Success: true } fullEntity)
        {
            _pendingHeader = new FullEntityPacket(
                int.Parse(fullEntity.Groups["id"].Value),
                fullEntity.Groups["cardId"].Success && fullEntity.Groups["cardId"].Value.Length > 0
                    ? fullEntity.Groups["cardId"].Value
                    : null);
            _pendingIndent = indent;
            return results;
        }

        if (FullEntityUpdateRegex.Match(content) is { Success: true } fullEntityUpdate)
        {
            var entityRef = EntityRef.Parse(fullEntityUpdate.Groups["entity"].Value);
            if (entityRef.Id is int id)
            {
                _pendingHeader = new FullEntityPacket(
                    id,
                    fullEntityUpdate.Groups["cardId"].Success && fullEntityUpdate.Groups["cardId"].Value.Length > 0
                        ? fullEntityUpdate.Groups["cardId"].Value
                        : null);
                _pendingIndent = indent;
            }
            return results;
        }

        if (ShowEntityRegex.Match(content) is { Success: true } showEntity)
        {
            _pendingHeader = new ShowEntityPacket(
                EntityRef.Parse(showEntity.Groups["entity"].Value),
                showEntity.Groups["cardId"].Success && showEntity.Groups["cardId"].Value.Length > 0
                    ? showEntity.Groups["cardId"].Value
                    : null);
            _pendingIndent = indent;
            return results;
        }

        if (content == "CREATE_GAME")
        {
            results.Add(new CreateGamePacket());
            return results;
        }

        if (PlayerMappingRegex.Match(content) is { Success: true } playerMapping)
        {
            results.Add(new PlayerMappingPacket(
                int.Parse(playerMapping.Groups["entityId"].Value),
                int.Parse(playerMapping.Groups["playerId"].Value)));
            return results;
        }

        if (BlockStartRegex.Match(content) is { Success: true } blockStart)
        {
            var entityToken = blockStart.Groups["entity"].Success ? blockStart.Groups["entity"].Value : null;
            results.Add(new BlockStartPacket(
                blockStart.Groups["blockType"].Value,
                entityToken is null ? null : EntityRef.Parse(entityToken)));
            return results;
        }

        if (content == "BLOCK_END")
        {
            results.Add(new BlockEndPacket());
            return results;
        }

        return results; // may just contain the flushed packet, or be empty
    }

    /// <summary>Flushes any in-progress FULL_ENTITY/SHOW_ENTITY block. Call once after the
    /// last line of a batch/file read, so a trailing block isn't left stuck pending.</summary>
    public LogPacket? FlushPending()
    {
        var pending = _pendingHeader;
        _pendingHeader = null;
        return pending;
    }
}