namespace HSBGTracker.Core.LogParsing;

/// <summary>
/// Tails a Power.log file, raising LineRead for each newly appended line. Uses polling
/// rather than relying on FileSystemWatcher alone - Hearthstone can write faster than FSW's
/// change events reliably fire, which is a common source of missed lines in naive tailers.
/// </summary>
public sealed class LogFileTailer : IDisposable
{
    private readonly Func<string?> _resolvePath;
    private readonly Timer _timer;
    private string? _path;
    private long _position;

    public event Action<string>? LineRead;

    /// <summary>Raised whenever the tailer switches to a different underlying file path -
    /// e.g. Hearthstone created a new session log folder after a reconnect.</summary>
    public event Action<string>? PathChanged;

    /// <param name="startAtEndOfFile">True to only read lines appended after this tailer
    /// starts (normal "live" mode). False to read the whole existing file first - useful
    /// for replaying a saved log for testing.</param>
    public LogFileTailer(string path, TimeSpan? pollInterval = null, bool startAtEndOfFile = true)
        : this(() => path, pollInterval, startAtEndOfFile)
    {
    }

    /// <summary>
    /// Instead of a fixed path, re-resolves the target file on every poll tick via
    /// <paramref name="resolvePath"/>. Use this when Hearthstone may create a new session
    /// log folder mid-session (e.g. on reconnect) so the tailer follows it instead of
    /// silently watching a now-abandoned file forever.
    /// </summary>
    public LogFileTailer(Func<string?> resolvePath, TimeSpan? pollInterval = null, bool startAtEndOfFile = true)
    {
        _resolvePath = resolvePath;
        _path = resolvePath();
        _position = startAtEndOfFile && _path is not null && File.Exists(_path) ? new FileInfo(_path).Length : 0;
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(200);
        _timer = new Timer(_ => Poll(), null, interval, interval);
    }

    private void Poll()
    {
        try
        {
            var resolved = _resolvePath();
            if (resolved is null) return;

            if (resolved != _path)
            {
                // Target file changed (e.g. new Hearthstone_<timestamp> session folder after
                // a reconnect) - switch to it and read it from the beginning, since it's a
                // brand-new log we haven't seen any of yet.
                _path = resolved;
                _position = 0;
                PathChanged?.Invoke(resolved);
            }

            if (!File.Exists(_path)) return;

            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Hearthstone truncates/rewrites Power.log on client restart - if the file is
            // now shorter than our last read position, start over from the beginning.
            if (stream.Length < _position)
                _position = 0;

            stream.Seek(_position, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                // Advance position for this line up front, and catch any exception a
                // downstream handler (parser/applier) throws while processing it. Without
                // this, an unhandled exception here would escape into the Timer callback and
                // silently terminate the process - which looks exactly like "tailing just
                // stopped" with no error shown, even though the file kept growing.
                try
                {
                    LineRead?.Invoke(line);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[error] LogFileTailer: failed to process line, skipping it: {ex.Message}");
                    Console.WriteLine($"[error] Offending line: {line}");
                }
            }

            _position = stream.Position;
        }
        catch (IOException)
        {
            // File momentarily locked by Hearthstone's own writer - just retry next tick.
        }
        catch (Exception ex)
        {
            // Catch-all so a single bad poll tick (e.g. an unexpected file state) can't
            // silently kill the Timer thread and stop tailing for the rest of the process.
            Console.WriteLine($"[error] LogFileTailer.Poll failed unexpectedly: {ex}");
        }
    }

    public void Dispose() => _timer.Dispose();
}
