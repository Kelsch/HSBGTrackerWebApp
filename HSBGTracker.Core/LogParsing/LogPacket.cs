namespace HSBGTracker.Core.LogParsing;

public abstract class LogPacket;

public sealed class CreateGamePacket : LogPacket;

/// <summary>A newly created entity, plus whichever "tag=X value=Y" lines followed it at
/// deeper indentation - Hearthstone prints an entity's initial tags this way rather than
/// as separate TAG_CHANGE lines.</summary>
public sealed class FullEntityPacket : LogPacket
{
    public int EntityId { get; }
    public string? CardId { get; }
    public List<(string TagName, string RawValue)> Tags { get; } = new();

    public FullEntityPacket(int entityId, string? cardId)
    {
        EntityId = entityId;
        CardId = cardId;
    }
}

/// <summary>An existing entity being revealed/updated (e.g. a card flipping face-up),
/// same trailing-tag-lines shape as FullEntityPacket.</summary>
public sealed class ShowEntityPacket : LogPacket
{
    public EntityRef Entity { get; }
    public string? CardId { get; }
    public List<(string TagName, string RawValue)> Tags { get; } = new();

    public ShowEntityPacket(EntityRef entity, string? cardId)
    {
        Entity = entity;
        CardId = cardId;
    }
}

/// <summary>Maps a Player game-object's EntityID to its PlayerID (1-8) - e.g. "Player
/// EntityID=20 PlayerID=7 GameAccountId=...". CONTROLLER tag values on other entities are
/// EntityIDs, while every other player reference in this codebase (bracket "player=N",
/// PLAYER_LEADERBOARD_PLACE, FriendlyPlayerId) uses PlayerID - this mapping is required to
/// translate between the two. Confirmed necessary against a real captured log where these
/// numbering spaces were being silently conflated, causing GetBoard to never match anything.</summary>
public sealed class PlayerMappingPacket : LogPacket
{
    public int EntityId { get; }
    public int PlayerId { get; }

    public PlayerMappingPacket(int entityId, int playerId)
    {
        EntityId = entityId;
        PlayerId = playerId;
    }
}

/// <summary>From GameState.DebugPrintGame() - PlayerID=N, PlayerName=Name#1234.
/// Opponent BattleTags often appear here even when Power TAG_CHANGEs only name the local player.</summary>
public sealed class PlayerNamePacket : LogPacket
{
    public int PlayerId { get; }
    public string Name { get; }

    public PlayerNamePacket(int playerId, string name)
    {
        PlayerId = playerId;
        Name = name;
    }
}

public sealed class TagChangePacket : LogPacket
{
    public EntityRef Entity { get; }
    public string TagName { get; }
    public string RawValue { get; }

    public TagChangePacket(EntityRef entity, string tagName, string rawValue)
    {
        Entity = entity;
        TagName = tagName;
        RawValue = rawValue;
    }
}

/// <summary>Reserved for combat-phase-boundary detection later - see GameStateApplier.</summary>
public sealed class BlockStartPacket : LogPacket
{
    public string BlockType { get; }
    public EntityRef? Source { get; }

    public BlockStartPacket(string blockType, EntityRef? source)
    {
        BlockType = blockType;
        Source = source;
    }
}

public sealed class BlockEndPacket : LogPacket;
