using HSBGTracker.Core.Snapshots;

namespace HSBGTrackerWebApp.Api.Contracts;

public sealed class RegisterUserRequest
{
    public string DisplayName { get; set; } = "";

    /// <summary>Their in-game BattleTag - lets other friends' uploads cross-link to this account
    /// when this person shows up as an opponent.</summary>
    public string? BattleTag { get; set; }

    public ResultVisibility? DefaultVisibility { get; set; }
}

public sealed class RegisterUserResponse
{
    public Guid UserId { get; set; }

    /// <summary>Shown exactly once - store it in the client app, it can't be retrieved again.</summary>
    public string ApiKey { get; set; } = "";
}
