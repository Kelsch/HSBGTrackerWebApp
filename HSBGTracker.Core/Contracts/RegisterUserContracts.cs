using HSBGTracker.Core.Snapshots;

namespace HSBGTracker.Core.Contracts;

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

/// <summary>Identity lookup for "whose API key is this" - used by the Web UI to greet the
/// right person and default the "just my games" filter.</summary>
public sealed class UserSummaryDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
}

/// <summary>Resolves an API key to an identity - what "sign in with an existing key" calls
/// to find out whose account it is.</summary>
public sealed class MeResponse
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
}
