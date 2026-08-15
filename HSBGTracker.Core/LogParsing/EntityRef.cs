using System.Text.RegularExpressions;

namespace HSBGTracker.Core.LogParsing;

/// <summary>
/// Represents an unresolved "Entity=" reference from a raw log line. The log refers to
/// entities three different ways: a plain numeric id ("Entity=68"), a bracketed descriptor
/// that embeds the id ("Entity=[name=... id=68 zone=PLAY ... player=1]"), or a bare token
/// that isn't numeric at all - "GameEntity", or a player's BattleTag-like name before its
/// numeric id has been established. Only the first two are resolvable on their own; the
/// third needs a name-to-id table built up elsewhere as the game reveals it.
/// </summary>
public sealed class EntityRef
{
    public string RawToken { get; }

    /// <summary>Resolved numeric id, if directly parseable from this token alone.</summary>
    public int? Id { get; }

    /// <summary>The owning player id, if present in a bracketed descriptor - e.g.
    /// "player=11" in "Entity=[entityName=... id=127 ... player=11]". Not present on a bare
    /// numeric or "GameEntity" token. Used as a fallback source for Entity.ControllerPlayerId
    /// since hero entities don't reliably carry a separate CONTROLLER TAG_CHANGE once they've
    /// moved to SETASIDE - confirmed against real captured logs.</summary>
    public int? PlayerId { get; }

    /// <summary>True when the token is a bare name (BattleTag / "Bartender Bob" / "GameEntity")
    /// that needs a name table to resolve - as opposed to a numeric id or bracket descriptor.</summary>
    public bool IsNamedToken => Id is null && !string.IsNullOrEmpty(RawToken);

    private EntityRef(string rawToken, int? id, int? playerId)
    {
        RawToken = rawToken;
        Id = id;
        PlayerId = playerId;
    }

    private static readonly Regex BracketIdRegex = new(@"\bid=(?<id>\d+)\b", RegexOptions.Compiled);
    private static readonly Regex BracketPlayerRegex = new(@"\bplayer=(?<player>\d+)\b", RegexOptions.Compiled);

    public static EntityRef Parse(string token)
    {
        token = token.Trim();

        if (int.TryParse(token, out var plainId))
            return new EntityRef(token, plainId, null);

        if (token.StartsWith('['))
        {
            var idMatch = BracketIdRegex.Match(token);
            var playerMatch = BracketPlayerRegex.Match(token);
            if (idMatch.Success)
            {
                return new EntityRef(
                    token,
                    int.Parse(idMatch.Groups["id"].Value),
                    playerMatch.Success ? int.Parse(playerMatch.Groups["player"].Value) : null);
            }
        }

        return new EntityRef(token, null, null);
    }
}
