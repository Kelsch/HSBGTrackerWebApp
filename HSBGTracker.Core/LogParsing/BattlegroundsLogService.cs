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

    private readonly GameStateApplier _applier;
    private readonly PowerLogLineParser _parser = new();
    private readonly object _stateLock = new();
    private LogFileTailer? _tailer;
    private readonly string? _powerLogPath;
    private readonly string? _hearthstoneInstallPath;

    public event Action<int, string, string>? TagChanged
    {
        add => _applier.TagChanged += value;
        remove => _applier.TagChanged -= value;
    }

    public BattlegroundsLogService(
        string? powerLogPath = null,
        string? zoneLogPath = null,
        bool startLiveTailing = true,
        string? hearthstoneInstallPath = null)
    {
        _applier = new GameStateApplier(State);
        _powerLogPath = powerLogPath;
        _hearthstoneInstallPath = hearthstoneInstallPath;

        // Intentionally do NOT tail or ReplayFile here.
        // Caller must Subscribe first, then call StartTailing().
        if (startLiveTailing)
            StartTailing();
    }

    /// <summary>Call only after event handlers are attached.</summary>
    public void StartTailing()
    {
        if (_tailer is not null)
            return;

        if (_powerLogPath is not null)
            _tailer = new LogFileTailer(_powerLogPath, startAtEndOfFile: true);
        else if (_hearthstoneInstallPath is not null)
            _tailer = new LogFileTailer(
                () => LogConfigWriter.FindLatestSessionPowerLog(_hearthstoneInstallPath)
                      ?? LogConfigWriter.DefaultPowerLogPath,
                startAtEndOfFile: true);
        else
            _tailer = new LogFileTailer(LogConfigWriter.DefaultPowerLogPath, startAtEndOfFile: true);

        _tailer.LineRead += OnLineRead;
        _tailer.PathChanged += OnPathChanged;

        var path = ResolveCurrentPath();
        if (path is not null && File.Exists(path))
        {
            Console.WriteLine($"[tracking] Catching up on existing log: {path}");
            ReplayFile(path);
        }
        else
            Console.WriteLine($"[tracking] No existing Power.log yet at: {path}");
    }

    private string? ResolveCurrentPath() =>
        _powerLogPath
        ?? (_hearthstoneInstallPath is not null
            ? LogConfigWriter.FindLatestSessionPowerLog(_hearthstoneInstallPath)
            : null)
        ?? LogConfigWriter.DefaultPowerLogPath;

    private void OnPathChanged(string path)
    {
        Console.WriteLine($"[diagnostic] Now tailing: {path}");
        // New session file only — full replay is OK (CREATE_GAME will Reset).
        if (File.Exists(path))
            ReplayFile(path);
    }

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
            OnLineRead(line);

        var trailing = _parser.FlushPending();
        if (trailing is not null)
        {
            lock (_stateLock)
                _applier.Apply(trailing);
        }
    }

    public void Dispose() => _tailer?.Dispose();
}