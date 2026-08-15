using System.Text.Json.Serialization;

namespace HSBGTrackerWebApp.Web.Services.Cards;

public sealed class HsbgCardListResponse
{
    public List<HsbgCard> Data { get; set; } = new();
    public HsbgPagination? Pagination { get; set; }
}

public sealed class HsbgPagination
{
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
    public int? NextOffset { get; set; }
}

public sealed class HsbgCard
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Text { get; set; }
    public string? Image { get; set; }
    public string? ImageGold { get; set; }
    public string? CardType { get; set; }
    public int? Tier { get; set; }
    public string? ExternalId { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<string> MinionTypes { get; set; } = new();
    public List<string> Keywords { get; set; } = new();

    [JsonIgnore]
    public string NormalizedExternalId =>
        string.IsNullOrWhiteSpace(ExternalId) ? "" : ExternalId.Trim();
}

/// <summary>Resolved display metadata for a Hearthstone cardId from a board snapshot.</summary>
public sealed class ResolvedCard
{
    public string CardId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public string CardType { get; init; } = "";
    public string? ImageUrl { get; init; }          // full card from hsbg.cards (heroes, trinkets, tooltips)
    public string? PortraitUrl { get; init; }        // ← NEW: clean portrait from HearthstoneJSON
    public int? Tier { get; init; }
    public int? HsbgId { get; init; }
    public bool IsGolden { get; init; }
    public bool IsDarkGift { get; init; }
    public bool IsTrinket { get; init; }
    public bool IsTrinketBuff { get; init; }
    public bool Found { get; init; }
}
