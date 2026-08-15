namespace HSBGTracker.Core.LogParsing;

/// <summary>
/// Ensures Hearthstone logging is configured for this tracker:
/// 1) AppData log.config - enables [Power]/[Zone]/[Bob] file logging
/// 2) Install-dir client.config - disables log file size cap so long BG games
///    are not truncated mid-match (FileSizeLimit.Int=-1).
/// Both are read at Hearthstone launch; restart the game after changes take effect.
/// </summary>
public static class LogConfigWriter
{
    private static readonly (string Section, string Key, string Value)[] RequiredLogConfigEntries =
    [
        ("Power", "LogLevel", "1"),
        ("Power", "FilePrinting", "true"),
        ("Power", "ConsolePrinting", "false"),
        ("Power", "ScreenPrinting", "false"),
        ("Zone", "LogLevel", "1"),
        ("Zone", "FilePrinting", "true"),
        ("Bob", "LogLevel", "1"),
        ("Bob", "FilePrinting", "true"),
    ];

    private static readonly (string Section, string Key, string Value)[] RequiredClientConfigEntries =
    [
        ("Log", "FileSizeLimit.Int", "-1"),
    ];

    public static string DefaultHearthstoneAppDataPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Blizzard", "Hearthstone");

    public static string DefaultPowerLogPath =>
        Path.Combine(DefaultHearthstoneAppDataPath, "Logs", "Power.log");

    public static string DefaultZoneLogPath =>
        Path.Combine(DefaultHearthstoneAppDataPath, "Logs", "Zone.log");

    /// <summary>
    /// Result of ensuring log.config / client.config on disk.
    /// </summary>
    public sealed record EnsureResult(
        bool LogConfigOk,
        bool LogConfigChanged,
        string LogConfigPath,
        bool ClientConfigOk,
        bool ClientConfigChanged,
        string? ClientConfigPath,
        string? ClientConfigError)
    {
        public bool NeedsHearthstoneRestart => LogConfigChanged || ClientConfigChanged;
        public bool AllOk => LogConfigOk && ClientConfigOk;
    }

    /// <summary>
    /// Picks the newest session folder under Logs that actually contains Power.log.
    /// </summary>
    public static string? FindLatestSessionPowerLog(string hearthstoneInstallPath) =>
        FindLatestSessionLogFile(hearthstoneInstallPath, "Power.log");

    /// <summary>
    /// Same as <see cref="FindLatestSessionPowerLog"/> but for Zone.log.
    /// </summary>
    public static string? FindLatestSessionZoneLog(string hearthstoneInstallPath) =>
        FindLatestSessionLogFile(hearthstoneInstallPath, "Zone.log");

    private static string? FindLatestSessionLogFile(string hearthstoneInstallPath, string fileName)
    {
        var logsDir = Path.Combine(hearthstoneInstallPath, "Logs");
        if (!Directory.Exists(logsDir)) return null;

        foreach (var session in new DirectoryInfo(logsDir)
                     .GetDirectories()
                     .OrderByDescending(d => d.LastWriteTimeUtc))
        {
            var candidate = Path.Combine(session.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Ensures AppData log.config and (when install path is known) install-dir client.config.
    /// Merges required keys; does not strip unrelated sections other tools may have added.
    /// </summary>
    /// <param name="hearthstoneInstallPath">
    /// Folder containing Hearthstone.exe (for client.config). Optional if only log.config is needed.
    /// </param>
    /// <param name="hearthstoneAppDataPath">Override for AppData Hearthstone folder; null = default.</param>
    public static EnsureResult EnsureConfigured(
        string? hearthstoneInstallPath = null,
        string? hearthstoneAppDataPath = null)
    {
        var appData = hearthstoneAppDataPath ?? DefaultHearthstoneAppDataPath;
        var logConfigPath = Path.Combine(appData, "log.config");

        var logOk = false;
        var logChanged = false;
        try
        {
            Directory.CreateDirectory(appData);
            logChanged = EnsureIniFile(logConfigPath, RequiredLogConfigEntries);
            logOk = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            // logOk stays false; caller reports path
        }

        bool clientOk;
        var clientChanged = false;
        string? clientPath = null;
        string? clientError = null;

        if (string.IsNullOrWhiteSpace(hearthstoneInstallPath))
        {
            clientOk = false;
            clientError =
                "HearthstoneInstallPath is not set - cannot write client.config " +
                "(needed for FileSizeLimit.Int=-1 so logs are not truncated).";
        }
        else if (!Directory.Exists(hearthstoneInstallPath))
        {
            clientOk = false;
            clientError = $"Hearthstone install folder not found: {hearthstoneInstallPath}";
        }
        else
        {
            clientPath = Path.Combine(hearthstoneInstallPath, "client.config");
            try
            {
                clientChanged = EnsureIniFile(clientPath, RequiredClientConfigEntries);
                clientOk = true;
            }
            catch (UnauthorizedAccessException)
            {
                clientOk = false;
                clientError =
                    $"Access denied writing {clientPath}. " +
                    "Run this app once as Administrator, or manually create client.config next to " +
                    "Hearthstone.exe containing:\n[Log]\nFileSizeLimit.Int=-1";
            }
            catch (Exception ex) when (ex is IOException or DirectoryNotFoundException)
            {
                clientOk = false;
                clientError = $"Could not write {clientPath}: {ex.Message}";
            }
        }

        return new EnsureResult(
            LogConfigOk: logOk,
            LogConfigChanged: logChanged,
            LogConfigPath: logConfigPath,
            ClientConfigOk: clientOk,
            ClientConfigChanged: clientChanged,
            ClientConfigPath: clientPath,
            ClientConfigError: clientError);
    }

    /// <summary>
    /// Ensures each required section/key/value exists. Returns true if the file was created or modified.
    /// </summary>
    private static bool EnsureIniFile(
        string path,
        IReadOnlyList<(string Section, string Key, string Value)> required)
    {
        var sections = File.Exists(path)
            ? ParseIni(File.ReadAllText(path))
            : new Dictionary<string, List<(string Key, string Value)>>(StringComparer.OrdinalIgnoreCase);

        var changed = !File.Exists(path);

        foreach (var (section, key, value) in required)
        {
            if (!sections.TryGetValue(section, out var entries))
            {
                entries = [];
                sections[section] = entries;
                changed = true;
            }

            var idx = entries.FindIndex(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
            {
                entries.Add((key, value));
                changed = true;
            }
            else if (!entries[idx].Value.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                entries[idx] = (entries[idx].Key, value);
                changed = true;
            }
        }

        if (changed)
            File.WriteAllText(path, FormatIni(sections));

        return changed;
    }

    private static Dictionary<string, List<(string Key, string Value)>> ParseIni(string text)
    {
        var sections = new Dictionary<string, List<(string Key, string Value)>>(StringComparer.OrdinalIgnoreCase);
        var current = ""; // keys before any [section]
        sections[current] = [];

        foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']') && line.Length >= 2)
            {
                current = line[1..^1].Trim();
                if (!sections.ContainsKey(current))
                    sections[current] = [];
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            sections[current].Add((key, value));
        }

        return sections;
    }

    private static string FormatIni(Dictionary<string, List<(string Key, string Value)>> sections)
    {
        var sb = new System.Text.StringBuilder();

        // Prefatory keys (no section), if any
        if (sections.TryGetValue("", out var root) && root.Count > 0)
        {
            foreach (var (k, v) in root)
                sb.Append(k).Append('=').Append(v).AppendLine();
            sb.AppendLine();
        }

        foreach (var (name, entries) in sections)
        {
            if (name.Length == 0) continue;
            sb.Append('[').Append(name).Append(']').AppendLine();
            foreach (var (k, v) in entries)
                sb.Append(k).Append('=').Append(v).AppendLine();
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
