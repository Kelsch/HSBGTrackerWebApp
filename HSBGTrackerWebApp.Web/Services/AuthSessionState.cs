using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace HSBGTrackerWebApp.Web.Services;

/// <summary>
/// Per-circuit session state: which friend is currently using this browser tab, and their
/// API key for calling the backend. Persisted to encrypted browser local storage so a
/// page refresh does not require re-entering the key.
/// </summary>
public sealed class AuthSessionState
{
    private const string StorageKey = "hsbg.authSession";

    private readonly ProtectedLocalStorage _localStorage;
    private bool _restoreAttempted;

    public AuthSessionState(ProtectedLocalStorage localStorage)
    {
        _localStorage = localStorage;
    }

    public string? ApiKey { get; private set; }
    public Guid? UserId { get; private set; }
    public string? DisplayName { get; private set; }

    public bool IsConnected => ApiKey is not null;

    /// <summary>True after the first successful attempt to read browser storage (or when JS is unavailable and we gave up for this circuit).</summary>
    public bool HasCheckedStorage => _restoreAttempted;

    public event Action? Changed;

    public async Task SetSessionAsync(string apiKey, Guid userId, string displayName)
    {
        ApiKey = apiKey;
        UserId = userId;
        DisplayName = displayName;
        await PersistAsync();
        Changed?.Invoke();
    }

    public async Task ClearAsync()
    {
        ApiKey = null;
        UserId = null;
        DisplayName = null;
        try
        {
            await _localStorage.DeleteAsync(StorageKey);
        }
        catch (InvalidOperationException)
        {
            // JS interop not available yet (prerender).
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Loads a previously saved session from browser storage. Safe to call multiple times;
    /// only the first successful check counts. Call from OnAfterRenderAsync so JS is available.
    /// </summary>
    public async Task EnsureRestoredAsync()
    {
        if (_restoreAttempted)
        {
            return;
        }

        try
        {
            var result = await _localStorage.GetAsync<StoredSession>(StorageKey);
            if (result.Success && result.Value is { } stored
                && string.IsNullOrWhiteSpace(stored.ApiKey) == false)
            {
                ApiKey = stored.ApiKey;
                UserId = stored.UserId;
                DisplayName = stored.DisplayName;
            }

            _restoreAttempted = true;
            Changed?.Invoke();
        }
        catch (InvalidOperationException)
        {
            // Prerender / no circuit yet — caller should retry after first render.
        }
    }

    private async Task PersistAsync()
    {
        if (ApiKey is null || UserId is null || DisplayName is null)
        {
            return;
        }

        try
        {
            await _localStorage.SetAsync(StorageKey, new StoredSession(ApiKey, UserId.Value, DisplayName));
            _restoreAttempted = true;
        }
        catch (InvalidOperationException)
        {
            // JS interop not available yet.
        }
    }

    private sealed record StoredSession(string ApiKey, Guid UserId, string DisplayName);
}
