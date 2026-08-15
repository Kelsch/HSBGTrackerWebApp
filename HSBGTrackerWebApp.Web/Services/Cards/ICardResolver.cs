namespace HSBGTrackerWebApp.Web.Services.Cards;

public interface ICardResolver
{
    Task EnsureReadyAsync(CancellationToken cancellationToken = default);

    Task<ResolvedCard> ResolveAsync(string? cardId, bool isGolden = false, CancellationToken cancellationToken = default);

    ResolvedCard Resolve(string? cardId, bool isGolden = false);

    string BuildImageUrl(int hsbgId, bool golden = false, string size = "medium");
}
