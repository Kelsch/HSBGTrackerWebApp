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

        if (startLiveTailing && _tailer is not null)
        {
            // Resolve path once so we can backfill.
            var path = powerLogPath
                ?? (hearthstoneInstallPath is not null
                    ? LogConfigWriter.FindLatestSessionPowerLog(hearthstoneInstallPath)
                    : null)
                ?? LogConfigWriter.DefaultPowerLogPath;

            if (path is not null && File.Exists(path))
            {
                Console.WriteLine($"[tracking] Catching up on existing log: {path}");
                ReplayFile(path);   // uses OnLineRead → full state build
                                    // Tailer was started with startAtEndOfFile=true, so it will only
                                    // deliver *new* lines from here on. Position is already at EOF.
            }

            _tailer.LineRead += OnLineRead;
            _tailer.PathChanged += path =>
            {
                Console.WriteLine($"[diagnostic] Now tailing: {path}");
                // New session folder after reconnect → full replay of the new file.
                if (File.Exists(path))
                    ReplayFile(path);
            };
        }
    }

    private readonly object _stateLock = new();

    private void OnLineRead(string line)
    {
        lock (_stateLock)
        {
            TotalLinesProcessed++;
            foreach (var packet in _parser.ParseLine(line))
            {
                RecognizedPackets++;
                _applier.Apply(packet);
            }
        }
    }

    public void ReplayFile(string powerLogPath, string? zoneLogPath = null)
    {
        foreach (var line in File.ReadLines(powerLogPath))
        {
            OnLineRead(line);
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