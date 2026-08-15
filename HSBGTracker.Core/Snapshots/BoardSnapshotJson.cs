using System.Text.Json;
using System.Text.Json.Serialization;

namespace HSBGTracker.Core.Snapshots;

/// <summary>
/// Shared JSON options and null-collection guards for board snapshot persistence.
/// Old rows may omit newer fields; clients may send camelCase or PascalCase.
/// </summary>
public static class BoardSnapshotJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        return options;
    }

    public static string Serialize(BoardSnapshot board) =>
        JsonSerializer.Serialize(Normalize(board), Options);

    public static BoardSnapshot Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new BoardSnapshot();

        try
        {
            var board = JsonSerializer.Deserialize<BoardSnapshot>(json, Options);
            return Normalize(board ?? new BoardSnapshot());
        }
        catch (JsonException)
        {
            return new BoardSnapshot();
        }
    }

    public static BoardSnapshot Normalize(BoardSnapshot board)
    {
        board.Minions ??= new List<MinionSnapshot>();
        board.Trinkets ??= new List<TrinketSnapshot>();
        board.HeroPower = NormalizeHeroPower(board.HeroPower);

        foreach (var minion in board.Minions)
            NormalizeMinion(minion);

        foreach (var trinket in board.Trinkets)
            NormalizeTrinket(trinket);

        return board;
    }

    private static HeroPowerSnapshot? NormalizeHeroPower(HeroPowerSnapshot? heroPower)
    {
        if (heroPower is null)
            return null;

        heroPower.CardId ??= "";
        heroPower.Name ??= "";
        return heroPower;
    }

    private static void NormalizeMinion(MinionSnapshot minion)
    {
        minion.CardId ??= "";
        minion.Name ??= "";
        minion.Attachments ??= new List<AttachedEntitySnapshot>();
        minion.Tags ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Recover golden/keywords from tags when older payloads only have the tag bag.
        if (!minion.IsGolden && minion.Tags.TryGetValue("PREMIUM", out var premium) && premium == 1)
            minion.IsGolden = true;
        if (!minion.Taunt && TagTruthy(minion.Tags, "TAUNT"))
            minion.Taunt = true;
        if (!minion.DivineShield && TagTruthy(minion.Tags, "DIVINE_SHIELD"))
            minion.DivineShield = true;
        if (!minion.Poisonous && (TagTruthy(minion.Tags, "POISONOUS") || TagTruthy(minion.Tags, "VENOMOUS")))
            minion.Poisonous = true;
        if (!minion.Reborn && TagTruthy(minion.Tags, "REBORN"))
            minion.Reborn = true;
        if (!minion.Windfury && (TagTruthy(minion.Tags, "WINDFURY") || TagTruthy(minion.Tags, "MEGA_WINDFURY")))
            minion.Windfury = true;
        if (!minion.Lifesteal && TagTruthy(minion.Tags, "LIFESTEAL"))
            minion.Lifesteal = true;
        if (!minion.HasDeathrattle && TagTruthy(minion.Tags, "DEATHRATTLE"))
            minion.HasDeathrattle = true;
        if (!minion.HasBattlecry && TagTruthy(minion.Tags, "BATTLECRY"))
            minion.HasBattlecry = true;

        foreach (var attachment in minion.Attachments)
            NormalizeAttachment(attachment);
    }

    private static void NormalizeTrinket(TrinketSnapshot trinket)
    {
        trinket.CardId ??= "";
        trinket.Name ??= "";
        trinket.Attachments ??= new List<AttachedEntitySnapshot>();
        trinket.Tags ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var attachment in trinket.Attachments)
            NormalizeAttachment(attachment);
    }

    private static void NormalizeAttachment(AttachedEntitySnapshot attachment)
    {
        attachment.CardId ??= "";
        attachment.Name ??= "";
        attachment.CardType ??= "";
        attachment.Tags ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TagTruthy(Dictionary<string, int> tags, string key) =>
        tags.TryGetValue(key, out var value) && value != 0;
}
