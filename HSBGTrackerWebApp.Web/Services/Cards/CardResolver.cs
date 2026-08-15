namespace HSBGTrackerWebApp.Web.Services.Cards;

public sealed class CardResolver : ICardResolver
{
    private readonly HsbgCardsClient _client;
    private readonly string _imageBaseUrl;

    public CardResolver(HsbgCardsClient client, IConfiguration configuration)
    {
        _client = client;
        _imageBaseUrl = (configuration["HsbgCards:BaseUrl"] ?? "https://hsbg.cards").TrimEnd('/');
    }

    public Task EnsureReadyAsync(CancellationToken cancellationToken = default) =>
        _client.EnsureLoadedAsync(cancellationToken);

    public async Task<ResolvedCard> ResolveAsync(string? cardId, bool isGolden = false, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        return await ResolveCoreAsync(cardId, isGolden, cancellationToken).ConfigureAwait(false);
    }

    public ResolvedCard Resolve(string? cardId, bool isGolden = false) =>
        // Catalog loads use ConfigureAwait(false); safe to block if called outside the render path.
        ResolveCoreAsync(cardId, isGolden).ConfigureAwait(false).GetAwaiter().GetResult();

    public string BuildImageUrl(int hsbgId, bool golden = false, string size = "medium")
    {
        var url = $"{_imageBaseUrl}/api/v1/cards/{hsbgId}/image?size={Uri.EscapeDataString(size)}";
        if (golden)
            url += "&golden=true";
        return url;
    }

    private async Task<ResolvedCard> ResolveCoreAsync(string? cardId, bool isGolden, CancellationToken cancellationToken = default)
    {
        var rawId = (cardId ?? "").Trim();
        if (rawId.Length == 0)
        {
            return new ResolvedCard
            {
                CardId = "",
                Name = "Unknown",
                Found = false,
            };
        }

        var golden = isGolden || rawId.EndsWith("_G", StringComparison.OrdinalIgnoreCase);
        HsbgCard? match = null;
        string? matchedKey = null;

        foreach (var candidate in CandidateExternalIds(rawId))
        {
            match = await _client.FindByExternalIdAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (match is not null)
            {
                matchedKey = candidate;
                break;
            }
        }

        if (match is null)
        {
            return new ResolvedCard
            {
                CardId = rawId,
                Name = rawId,
                CardType = GuessTypeFromId(rawId),
                IsGolden = golden,
                IsDarkGift = IsDarkGiftId(rawId),
                IsTrinket = IsTrinketId(rawId),
                IsTrinketBuff = IsTrinketBuffId(rawId),
                PortraitUrl = BuildPortraitUrl(rawId),
                Found = false,
            };
        }

        var cardType = match.CardType ?? GuessTypeFromId(rawId);
        var isTrinket = string.Equals(cardType, "trinket", StringComparison.OrdinalIgnoreCase) || IsTrinketId(rawId);
        var isDarkGift = IsDarkGiftId(rawId) || IsDarkGiftId(matchedKey) || HasDarkGiftCategory(match);
        var isTrinketBuff = IsTrinketBuffId(rawId) || IsTrinketBuffId(matchedKey);

        var displayName = string.IsNullOrWhiteSpace(match.Name) ? rawId : match.Name.Trim();

        return new ResolvedCard
        {
            CardId = rawId,
            Name = displayName,
            Text = match.Text ?? "",
            CardType = cardType,
            ImageUrl = BuildImageUrl(match.Id, golden),
            PortraitUrl = BuildPortraitUrl(rawId),
            Tier = match.Tier,
            HsbgId = match.Id,
            IsGolden = golden,
            IsDarkGift = isDarkGift,
            IsTrinket = isTrinket,
            IsTrinketBuff = isTrinketBuff,
            Found = true,
        };
    }

    private static string? BuildPortraitUrl(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            return null;

        // Strip common golden / enchantment suffixes so we hit the real portrait
        var id = cardId.Trim();
        if (id.EndsWith("_G", StringComparison.OrdinalIgnoreCase))
            id = id[..^2];
        if (id.EndsWith("_Ge", StringComparison.OrdinalIgnoreCase))
            id = id[..^3];
        if (id.EndsWith("e", StringComparison.Ordinal) && id.Length > 3)
            id = id[..^1];

        return $"https://art.hearthstonejson.com/v1/512x/{id}.webp";
    }

    private static IEnumerable<string> CandidateExternalIds(string cardId)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            var v = value.Trim();
            if (seen.Add(v))
                list.Add(v);
        }

        Add(cardId);

        // Golden minions in the log usually end with _G.
        if (cardId.EndsWith("_G", StringComparison.OrdinalIgnoreCase))
            Add(cardId[..^2]);

        // Enchantment tokens often append 'e' (and occasionally '_Ge') to a known card/effect id.
        if (cardId.EndsWith("e", StringComparison.Ordinal) && cardId.Length > 1)
        {
            Add(cardId[..^1]);
            if (cardId.EndsWith("_Ge", StringComparison.OrdinalIgnoreCase) && cardId.Length > 3)
                Add(cardId[..^3]);
        }

        return list;
    }

    private static string GuessTypeFromId(string cardId)
    {
        if (IsTrinketId(cardId) && !IsTrinketBuffId(cardId))
            return "trinket";
        if (cardId.Contains("HERO", StringComparison.OrdinalIgnoreCase)
            || cardId.Contains("BaconShop_HERO", StringComparison.OrdinalIgnoreCase))
            return "hero";
        if (cardId.Contains("HP_", StringComparison.OrdinalIgnoreCase)
            || cardId.Contains("HeroPower", StringComparison.OrdinalIgnoreCase))
            return "hero_power";
        if (cardId.EndsWith("e", StringComparison.Ordinal) || cardId.Contains("enchant", StringComparison.OrdinalIgnoreCase))
            return "enchantment";
        if (IsDarkGiftId(cardId))
            return "spell";
        return "";
    }

    private static bool IsDarkGiftId(string? cardId) =>
        !string.IsNullOrEmpty(cardId)
        && (cardId.Contains("MidGameEffect", StringComparison.OrdinalIgnoreCase)
            || cardId.Contains("DarkGift", StringComparison.OrdinalIgnoreCase));

    private static bool IsTrinketId(string? cardId) =>
        !string.IsNullOrEmpty(cardId)
        && (cardId.Contains("MagicItem", StringComparison.OrdinalIgnoreCase)
            || cardId.Contains("Trinket", StringComparison.OrdinalIgnoreCase));

    private static bool IsTrinketBuffId(string? cardId) =>
        !string.IsNullOrEmpty(cardId)
        && cardId.Contains("MagicItem", StringComparison.OrdinalIgnoreCase)
        && (cardId.EndsWith("e", StringComparison.Ordinal)
            || cardId.Contains("MagicItem_", StringComparison.OrdinalIgnoreCase));

    private static bool HasDarkGiftCategory(HsbgCard card) =>
        card.Categories.Any(c =>
            c.Contains("dark", StringComparison.OrdinalIgnoreCase)
            || c.Contains("gift", StringComparison.OrdinalIgnoreCase)
            || c.Contains("midgame", StringComparison.OrdinalIgnoreCase));
}
