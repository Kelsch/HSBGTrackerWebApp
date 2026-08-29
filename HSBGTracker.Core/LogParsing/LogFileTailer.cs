using System.Text;

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

    private int _isPolling; // 0 = idle, 1 = running

    private void Poll()
    {
        if (Interlocked.CompareExchange(ref _isPolling, 1, 0) != 0)
            return;

        try
        {
            var resolved = _resolvePath();
            if (resolved is null) return;

            if (resolved != _path)
            {
                _path = resolved;
                _position = 0;
                _remainder = "";
                PathChanged?.Invoke(resolved);
            }

            if (_path is null || !File.Exists(_path)) return;

            using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            if (stream.Length < _position)
            {
                _position = 0;
                _remainder = "";
            }

            if (stream.Length == _position)
                return;

            stream.Seek(_position, SeekOrigin.Begin);

            // Read only the new slice; do not use StreamReader for position tracking.
            var toRead = stream.Length - _position;
            if (toRead > int.MaxValue) toRead = int.MaxValue;
            var buffer = new byte[toRead];
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0) return;

            var text = Encoding.UTF8.GetString(buffer, 0, read);

            // Keep a trailing partial line until a newline arrives.
            _remainder += text;

            var parts = _remainder.Split('\n');
            // If the chunk did not end with \n, last element is incomplete.
            var completeCount = _remainder.EndsWith('\n') ? parts.Length : parts.Length - 1;
            if (!_remainder.EndsWith('\n') && parts.Length > 0)
                _remainder = parts[^1];
            else
                _remainder = "";

            for (var i = 0; i < completeCount; i++)
            {
                var line = parts[i].TrimEnd('\r');
                if (line.Length == 0) continue;
                try
                {
                    LineRead?.Invoke(line);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[error] LogFileTailer line failed: {ex.Message}");
                }
            }

            // Advance only by what we consumed from the file this poll.
            _position += read;
        }
        catch (IOException)
        {
            // HS briefly locking the file — retry next tick.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[error] LogFileTailer.Poll: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _isPolling, 0);
        }
    }

    private string _remainder = "";

    public void Dispose() => _timer.Dispose();
}
