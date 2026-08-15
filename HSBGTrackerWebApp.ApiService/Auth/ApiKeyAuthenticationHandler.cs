using System.Security.Claims;
using System.Text.Encodings.Web;
using HSBGTrackerWebApp.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HSBGTrackerWebApp.Api.Auth;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions;

/// <summary>
/// Simple bearer-token auth: "Authorization: Bearer &lt;apiKey&gt;", checked against a hash
/// in the Users table. Deliberately lightweight - this is a small friend-group tool, not a
/// public service, so full OAuth/social login would be more infrastructure than the problem needs.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string SchemeName = "ApiKey";

    private readonly IUserRepository _users;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserRepository users)
        : base(options, logger, encoder)
    {
        _users = users;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header) ||
            !header.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = header.ToString()["Bearer ".Length..].Trim();
        if (apiKey.Length == 0)
            return AuthenticateResult.Fail("Empty API key.");

        var user = await _users.FindByApiKeyHashAsync(ApiKeyGenerator.Hash(apiKey));
        if (user is null)
            return AuthenticateResult.Fail("Invalid API key.");

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
            },
            SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
