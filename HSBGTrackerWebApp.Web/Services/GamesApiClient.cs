using System.Net.Http.Headers;
using System.Net.Http.Json;
using HSBGTracker.Core.Contracts;

namespace HSBGTrackerWebApp.Web.Services;

public sealed class GamesApiClient
{
    private readonly HttpClient _http;
    private readonly AuthSessionState _session;

    public GamesApiClient(HttpClient http, AuthSessionState session)
    {
        _http = http;
        _session = session;
    }

    private void ApplySessionAuth() =>
        _http.DefaultRequestHeaders.Authorization = _session.ApiKey is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _session.ApiKey);

    /// <summary>Resolves identity for a not-yet-connected API key - used on the Connect page
    /// before the session is established, so it takes the key explicitly rather than from session state.</summary>
    public async Task<UserSummaryDto?> WhoAmIAsync(string apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var response = await _http.SendAsync(request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<UserSummaryDto>()
            : null;
    }

    public async Task<RegisterUserResponse?> RegisterAsync(RegisterUserRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/users/register", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<RegisterUserResponse>()
            : null;
    }

    /// <param name="ownerUserId">Pass to filter down to one person's games; omit to see the
    /// full shared feed (everyone's public games, plus your own private ones).</param>
    public async Task<List<GameDto>> ListGamesAsync(Guid? ownerUserId = null, int page = 1, int pageSize = 25)
    {
        ApplySessionAuth();
        var url = $"/api/games?page={page}&pageSize={pageSize}" + (ownerUserId is null ? "" : $"&ownerUserId={ownerUserId}");
        var result = await _http.GetFromJsonAsync<List<GameDto>>(url);
        return result ?? new List<GameDto>();
    }

    public async Task<GameDto?> GetGameAsync(Guid id)
    {
        ApplySessionAuth();
        return await _http.GetFromJsonAsync<GameDto>($"/api/games/{id}");
    }
}
