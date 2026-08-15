using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;

namespace HSBGTrackerWebApp.Web.Services.Cards;

/// <summary>
/// Downloads and caches the hsbg.cards Battlegrounds catalog, indexed by externalId (log cardId).
/// </summary>
public sealed class HsbgCardsClient
{
    public const string HttpClientName = "HsbgCards";
    //private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HsbgCardsClient> _logger;
    private readonly string _cacheFilePath;
    private readonly TimeSpan _cacheDuration;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private ConcurrentDictionary<string, HsbgCard> _byExternalId =
        new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;
    private int _remoteTotal;
    private string _fingerprint = "";
    private bool _loaded;

    public HsbgCardsClient(
        IHttpClientFactory httpClientFactory,
        ILogger<HsbgCardsClient> logger,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        var configuredPath = configuration["HsbgCards:CachePath"];
        _cacheFilePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "hsbg-cards-cache.json")
            : configuredPath;

        var hours = configuration.GetValue("HsbgCards:CacheHours", 12);
        _cacheDuration = TimeSpan.FromHours(Math.Max(1, hours));
    }

    public bool IsLoaded => _loaded;
    public bool IsStale => !_loaded || DateTimeOffset.UtcNow - _loadedAt >= _cacheDuration;
    public int CachedCount => _byExternalId.Count;

    /// <summary>
    /// Ensures an in-memory catalog is available. Prefers disk; only hits the network
    /// when there is no usable local cache (first run).
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
            return;

        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded)
                return;

            if (TryLoadFromDisk())
            {
                _logger.LogInformation(
                    "Loaded {Count} hsbg.cards entries from disk cache ({Path}).",
                    _byExternalId.Count,
                    _cacheFilePath);
                return;
            }

            await DownloadAndPersistAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    /// Background refresh: skips work when the cache is still fresh, otherwise checks the
    /// remote catalog and redownloads only when it appears to have changed.
    /// </summary>
    public async Task RefreshIfNeededAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded && !IsStale)
            return;

        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded && !IsStale)
                return;

            if (!_loaded && TryLoadFromDisk() && !IsStale)
            {
                _logger.LogInformation(
                    "Loaded {Count} hsbg.cards entries from disk cache ({Path}).",
                    _byExternalId.Count,
                    _cacheFilePath);
                return;
            }

            if (_loaded && !await HasRemoteCatalogChangedAsync(cancellationToken).ConfigureAwait(false))
            {
                _loadedAt = DateTimeOffset.UtcNow;
                TrySaveToDisk();
                _logger.LogInformation("hsbg.cards catalog unchanged; refreshed cache timestamp only.");
                return;
            }

            await DownloadAndPersistAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Keep serving the existing cache if a background refresh fails.
            _logger.LogWarning(ex, "Failed to refresh hsbg.cards catalog; keeping existing cache.");
            if (!_loaded)
                throw;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task<HsbgCard?> FindByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            return null;

        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return _byExternalId.TryGetValue(externalId.Trim(), out var card) ? card : null;
    }

    private async Task DownloadAndPersistAsync(CancellationToken cancellationToken)
    {
        var (map, total, fingerprint) = await DownloadCatalogAsync(cancellationToken).ConfigureAwait(false);
        _byExternalId = map;
        _remoteTotal = total;
        _fingerprint = fingerprint;
        _loadedAt = DateTimeOffset.UtcNow;
        _loaded = true;
        TrySaveToDisk();
        _logger.LogInformation("Downloaded {Count} hsbg.cards entries into cache.", map.Count);
    }

    private async Task<bool> HasRemoteCatalogChangedAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            // First page is enough to detect size / content shifts.
            using var response = await client
                .GetAsync("api/v1/cards?pool=all&limit=25&offset=0", cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var page = await response.Content
                .ReadFromJsonAsync<HsbgCardListResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (page?.Data is null)
                return true;

            var total = page.Pagination?.Total ?? page.Data.Count;
            var fingerprint = BuildFingerprint(page.Data);
            return total != _remoteTotal || !string.Equals(fingerprint, _fingerprint, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not probe hsbg.cards for changes; forcing full download.");
            return true;
        }
    }

    private async Task<(ConcurrentDictionary<string, HsbgCard> Map, int Total, string Fingerprint)> DownloadCatalogAsync(
        CancellationToken cancellationToken)
    {
        var map = new ConcurrentDictionary<string, HsbgCard>(StringComparer.OrdinalIgnoreCase);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var offset = 0;
        const int limit = 100;
        var total = 0;
        var firstPage = new List<HsbgCard>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = $"api/v1/cards?pool=all&limit={limit}&offset={offset}";
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var page = await response.Content
                .ReadFromJsonAsync<HsbgCardListResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (page?.Data is null || page.Data.Count == 0)
                break;

            if (offset == 0)
            {
                firstPage.AddRange(page.Data.Take(25));
                total = page.Pagination?.Total ?? 0;
            }

            foreach (var card in page.Data)
            {
                if (string.IsNullOrWhiteSpace(card.ExternalId))
                    continue;
                map[card.ExternalId.Trim()] = card;
            }

            if (page.Pagination?.NextOffset is null)
                break;

            offset = page.Pagination.NextOffset.Value;
        }

        if (total == 0)
            total = map.Count;

        return (map, total, BuildFingerprint(firstPage));
    }

    private static string BuildFingerprint(IEnumerable<HsbgCard> cards) =>
        string.Join('|', cards
            .OrderBy(c => c.Id)
            .Take(25)
            .Select(c => $"{c.Id}:{c.ExternalId}"));

    private bool TryLoadFromDisk()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
                return false;

            var json = File.ReadAllText(_cacheFilePath);
            var disk = JsonSerializer.Deserialize<HsbgCardDiskCache>(json, JsonOptions);
            if (disk?.Cards is null || disk.Cards.Count == 0)
                return false;

            var map = new ConcurrentDictionary<string, HsbgCard>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in disk.Cards)
            {
                if (string.IsNullOrWhiteSpace(card.ExternalId))
                    continue;
                map[card.ExternalId.Trim()] = card;
            }

            if (map.IsEmpty)
                return false;

            _byExternalId = map;
            _loadedAt = disk.LoadedAtUtc == default ? DateTimeOffset.UtcNow : disk.LoadedAtUtc;
            _remoteTotal = disk.RemoteTotal;
            _fingerprint = disk.Fingerprint ?? "";
            _loaded = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed reading hsbg.cards disk cache at {Path}.", _cacheFilePath);
            return false;
        }
    }

    private void TrySaveToDisk()
    {
        try
        {
            var dir = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var disk = new HsbgCardDiskCache
            {
                LoadedAtUtc = _loadedAt,
                RemoteTotal = _remoteTotal,
                Fingerprint = _fingerprint,
                Cards = _byExternalId.Values
                    .OrderBy(c => c.Id)
                    .ToList(),
            };

            var json = JsonSerializer.Serialize(disk, JsonOptions);
            var tempPath = _cacheFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Copy(tempPath, _cacheFilePath, overwrite: true);
            File.Delete(tempPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed writing hsbg.cards disk cache at {Path}.", _cacheFilePath);
        }
    }

    private sealed class HsbgCardDiskCache
    {
        public DateTimeOffset LoadedAtUtc { get; set; }
        public int RemoteTotal { get; set; }
        public string? Fingerprint { get; set; }
        public List<HsbgCard> Cards { get; set; } = new();
    }
}
