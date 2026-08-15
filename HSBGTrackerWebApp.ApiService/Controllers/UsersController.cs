using System.Security.Claims;
using HSBGTrackerWebApp.Api.Auth;
using HSBGTrackerWebApp.Api.Data;
using HSBGTracker.Core.Contracts;
using HSBGTracker.Core.Snapshots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSBGTrackerWebApp.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserRepository _users;

    public UsersController(IUserRepository users) => _users = users;

    /// <summary>
    /// Creates a friend's account and returns their API key. Deliberately unauthenticated -
    /// this is the first thing a new friend does to get set up. The key is returned exactly
    /// once here; only its hash is stored, so there's no "forgot my key" recovery beyond
    /// issuing a new account.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<RegisterUserResponse>> Register(RegisterUserRequest request)
    {
        var apiKey = ApiKeyGenerator.Generate();
        var id = await _users.CreateAsync(
            request.DisplayName,
            request.BattleTag,
            ApiKeyGenerator.Hash(apiKey),
            request.DefaultVisibility ?? ResultVisibility.Public);

        return Ok(new RegisterUserResponse { UserId = id, ApiKey = apiKey });
    }

    /// <summary>Resolves the caller's identity from their API key - used by the Web UI on
    /// connect, to greet the right person and default filters to their own games.</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserSummaryDto> Me() => Ok(new UserSummaryDto
    {
        UserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        DisplayName = User.FindFirstValue(ClaimTypes.Name) ?? "",
    });
}
