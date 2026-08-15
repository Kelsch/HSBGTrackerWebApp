using HSBGTracker.Core.Model;

namespace HSBGTracker.Core.LogParsing;

/// <summary>
/// Wires LogFileTailer -> PowerLogLineParser -> GameStateApplier into one live service.
/// Construct one of these pointed at Power.log and read its State as the game progresses;
/// subscribe to State.PlayerEliminated for the win/loss trigger built earlier.
/// </summary>
public sealed class BattlegroundsLogService : IDisposable
{
    public GameState State { get; } = new();

    public int TotalLinesProcessed { get; private set; }
    public int RecognizedPackets { get; private set; }

    public event Action<int, string, string>? TagChanged
    {
        add => _applier.TagChanged += value;
        remove => _applier.TagChanged -= value;
    }

    private readonly LogFileTailer? _tailer;
    private readonly LogFileTailer? _zoneTailer;
    private readonly PowerLogLineParser _parser = new();
    private readonly ZoneLogLineParser _zoneParser = new();
    private readonly GameStateApplier _applier;

    /// <param name="powerLogPath">Fixed Power.log path override. If null and
    /// <paramref name="hearthstoneInstallPath"/> is given, the newest session folder under
    /// its Logs directory is re-resolved on every poll tick, so a reconnect mid-game (which
    /// creates a brand-new Hearthstone_&lt;timestamp&gt; folder) is followed automatically
    /// instead of silently tailing an abandoned file.</param>
    public BattlegroundsLogService(
        string? powerLogPath = null,
        string? zoneLogPath = null,
        bool startLiveTailing = true,
        string? hearthstoneInstallPath = null)
    {
        _applier = new GameStateApplier(State);

        if (startLiveTailing)
        {
            _tailer = powerLogPath is not null
                ? new LogFileTailer(powerLogPath)
                : hearthstoneInstallPath is not null
                    ? new LogFileTailer(() => LogConfigWriter.FindLatestSessionPowerLog(hearthstoneInstallPath) ?? LogConfigWriter.DefaultPowerLogPath)
                    : new LogFileTailer(LogConfigWriter.DefaultPowerLogPath);
            _tailer.LineRead += OnLineRead;
            _tailer.PathChanged += path => Console.WriteLine($"[diagnostic] Now tailing: {path}");

            // More reliable than the FULL_ENTITY hand-reveal heuristic - Zone.log labels the
            // friendly player explicitly via local=True, and works in Battlegrounds where hand
            // visibility doesn't behave like constructed Hearthstone.
            _zoneTailer = zoneLogPath is not null
                ? new LogFileTailer(zoneLogPath)
                : hearthstoneInstallPath is not null
                    ? new LogFileTailer(() => LogConfigWriter.FindLatestSessionZoneLog(hearthstoneInstallPath) ?? LogConfigWriter.DefaultZoneLogPath)
                    : new LogFileTailer(LogConfigWriter.DefaultZoneLogPath);
            _zoneTailer.LineRead += OnZoneLineRead;
        }
    }


    private void OnLineRead(string line)
    {
        TotalLinesProcessed++;
        foreach (var packet in _parser.ParseLine(line))
        {
            RecognizedPackets++;
            _applier.Apply(packet);
        }
    }

    private void OnZoneLineRead(string line)
    {
        if (State.FriendlyPlayerId is null)
        {
            var friendlyId = _zoneParser.TryGetFriendlyPlayerId(line);
            if (friendlyId is not null)
                State.FriendlyPlayerId = friendlyId;
        }
    }

    public void ReplayFile(string powerLogPath, string? zoneLogPath = null)
    {
        foreach (var line in File.ReadLines(powerLogPath))
        {
            OnLineRead(line);
        }

        if (zoneLogPath is not null)
        {
            foreach (var line in File.ReadLines(zoneLogPath))
            {
                OnZoneLineRead(line);
            }
        }

        var trailing = _parser.FlushPending();
        if (trailing is not null)
        {
            _applier.Apply(trailing);
        }
    }

    public void Dispose()
    {
        _tailer?.Dispose();
        _zoneTailer?.Dispose();
    }
}