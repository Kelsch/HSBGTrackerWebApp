namespace HSBGTrackerWebApp.Web.Services.Cards;

/// <summary>
/// Warms the card catalog at startup and periodically refreshes it so game pages never
/// block on a full remote download after the first successful cache write.
/// </summary>
public sealed class HsbgCardsRefreshService : BackgroundService
{
    private readonly HsbgCardsClient _client;
    private readonly ILogger<HsbgCardsRefreshService> _logger;
    private readonly TimeSpan _interval;

    public HsbgCardsRefreshService(
        HsbgCardsClient client,
        ILogger<HsbgCardsRefreshService> logger,
        IConfiguration configuration)
    {
        _client = client;
        _logger = logger;
        var hours = configuration.GetValue("HsbgCards:RefreshCheckHours", 6);
        _interval = TimeSpan.FromHours(Math.Max(1, hours));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Immediate warm / refresh on startup.
        await SafeRefreshAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            await SafeRefreshAsync(stoppingToken).ConfigureAwait(false);
    }

    private async Task SafeRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.RefreshIfNeededAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug(
                "Card catalog ready. Count={Count}, Stale={Stale}",
                _client.CachedCount,
                _client.IsStale);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background hsbg.cards refresh failed.");
        }
    }
}