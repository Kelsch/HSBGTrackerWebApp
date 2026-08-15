namespace HSBGTracker.Core.CardData;

/// <summary>
/// Looks up static card reference data by CardId. The scorer depends on this abstraction
/// rather than a concrete loader, so where the data actually comes from (a bundled JSON
/// file, a fetch from HearthstoneJSON, a cached local copy) is an implementation detail
/// you can swap in later without touching scoring logic.
/// </summary>
public interface ICardDatabase
{
    CardDefinition? Find(string cardId);
}

/// <summary>
/// Simplest possible implementation - holds everything in memory, keyed by CardId.
/// Populate it however you like (e.g. deserialize a HearthstoneJSON export filtered to
/// BATTLEGROUNDS cards and map into CardDefinition) - that ingestion step isn't built yet.
/// </summary>
public sealed class InMemoryCardDatabase : ICardDatabase
{
    private readonly Dictionary<string, CardDefinition> _cardsByCardId;

    public InMemoryCardDatabase(IEnumerable<CardDefinition> cards) =>
        _cardsByCardId = cards.ToDictionary(c => c.CardId, StringComparer.OrdinalIgnoreCase);

    public CardDefinition? Find(string cardId) =>
        _cardsByCardId.TryGetValue(cardId, out var def) ? def : null;
}
