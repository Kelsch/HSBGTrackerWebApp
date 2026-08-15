using System.Security.Claims;
using HSBGTrackerWebApp.Api.Data;
using HSBGTrackerWebApp.Api.Services;
using HSBGTracker.Core.Contracts;
using HSBGTracker.Core.Snapshots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSBGTrackerWebApp.Api.Controllers;

[ApiController]
[Route("api/games")]
[Authorize]
public sealed class GamesController : ControllerBase
{
    private readonly IGameRepository _games;
    private readonly IUserRepository _users;
    private readonly IOpponentLinkingService _opponentLinking;

    public GamesController(IGameRepository games, IUserRepository users, IOpponentLinkingService opponentLinking)
    {
        _games = games;
        _users = users;
        _opponentLinking = opponentLinking;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost]
    public async Task<ActionResult<Guid>> Upload(UploadGameRequest request)
    {
        var caller = await _users.FindByIdAsync(CurrentUserId);
        if (caller is null) return Unauthorized();

        var opponentOwnerUserId = await _opponentLinking.TryLinkAsync(request.OpponentPlayerName);

        var myBoard = BoardSnapshotJson.Normalize(request.MyBoard);
        var opponentBoard = BoardSnapshotJson.Normalize(request.OpponentBoard);

        var record = new GameRecord
        {
            ClientGameId = request.ClientGameId,
            OwnerUserId = CurrentUserId,
            Visibility = request.Visibility ?? caller.DefaultVisibility,
            PlayedAtUtc = request.PlayedAtUtc,
            Placement = request.Placement,
            MyBoardJson = BoardSnapshotJson.Serialize(myBoard),
            MyBoardScore = myBoard.Score,
            OpponentBoardJson = BoardSnapshotJson.Serialize(opponentBoard),
            OpponentBoardScore = opponentBoard.Score,
            OpponentPlayerName = request.OpponentPlayerName,
            OpponentOwnerUserId = opponentOwnerUserId,
        };

        var id = await _games.UpsertAsync(record);
        return Ok(id);
    }

    /// <param name="ownerUserId">Omit to see everyone's public games plus your own private ones;
    /// pass your own id for the "just show my games" filter.</param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GameDto>>> List(
        [FromQuery] Guid? ownerUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        var filter = new GameListFilter(CurrentUserId, ownerUserId, page, pageSize);
        var rows = await _games.ListAsync(filter);
        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GameDto>> Get(Guid id)
    {
        var row = await _games.GetByIdAsync(id, CurrentUserId);
        if (row is null) return NotFound();
        return Ok(ToDto(row));
    }

    private static GameDto ToDto(GameRecord row) => new()
    {
        Id = row.Id,
        OwnerUserId = row.OwnerUserId,
        OwnerDisplayName = row.OwnerDisplayName,
        Visibility = row.Visibility,
        PlayedAtUtc = row.PlayedAtUtc,
        Placement = row.Placement,
        MyBoard = BoardSnapshotJson.Deserialize(row.MyBoardJson),
        MyBoardScore = row.MyBoardScore,
        OpponentBoard = BoardSnapshotJson.Deserialize(row.OpponentBoardJson),
        OpponentBoardScore = row.OpponentBoardScore,
        OpponentPlayerName = row.OpponentPlayerName,
        OpponentOwnerUserId = row.OpponentOwnerUserId,
        OpponentOwnerDisplayName = row.OpponentOwnerDisplayName,
    };
}
