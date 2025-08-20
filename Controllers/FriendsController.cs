using System.Security.Claims;
using JaeZoo.Server.Data;
using JaeZoo.Server.Dtos;
using JaeZoo.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JaeZoo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly AppDbContext _db;
    public FriendsController(AppDbContext db) => _db = db;

    private bool TryGetUserId(out Guid userId)
    {
        var idStr = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idStr, out userId);
    }

    private Task<bool> AlreadyFriendsOrPending(Guid me, Guid other) =>
        _db.Friendships.AnyAsync(f =>
            (f.RequesterId == me && f.AddresseeId == other) ||
            (f.RequesterId == other && f.AddresseeId == me));

    [HttpGet("list")]
    public async Task<ActionResult<IEnumerable<FriendDto>>> List()
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        var accepted = _db.Friendships.Where(f => f.Status == FriendshipStatus.Accepted &&
                                                   (f.RequesterId == me || f.AddresseeId == me));

        var asRequester = accepted.Where(f => f.RequesterId == me)
            .Join(_db.Users, f => f.AddresseeId, u => u.Id,
                (f, u) => new FriendDto(u.Id.ToString(), u.UserName, u.Email));

        var asAddressee = accepted.Where(f => f.AddresseeId == me)
            .Join(_db.Users, f => f.RequesterId, u => u.Id,
                (f, u) => new FriendDto(u.Id.ToString(), u.UserName, u.Email));

        var items = await asRequester.Concat(asAddressee)
            .OrderBy(x => x.UserName)
            .ToListAsync();

        return Ok(items);
    }

    // Было Request(...) — переименовали, чтобы не перекрывать ControllerBase.Request
    [HttpPost("request/{userId:guid}")]
    public async Task<IActionResult> SendRequest(Guid userId)
    {
        if (!TryGetUserId(out var me)) return Unauthorized();
        if (userId == me) return BadRequest("Нельзя отправить заявку самому себе.");

        if (await AlreadyFriendsOrPending(me, userId))
            return Conflict("Заявка уже отправлена или вы уже друзья.");

        var f = new Friendship
        {
            RequesterId = me,
            AddresseeId = userId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.Friendships.Add(f);
        await _db.SaveChangesAsync();
        return Ok(new { requested = true });
    }

    [HttpPost("accept/{userId:guid}")]
    public async Task<IActionResult> Accept(Guid userId)
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        var fr = await _db.Friendships.SingleOrDefaultAsync(f =>
            f.RequesterId == userId && f.AddresseeId == me && f.Status == FriendshipStatus.Pending);

        if (fr == null) return NotFound();

        fr.Status = FriendshipStatus.Accepted;
        await _db.SaveChangesAsync();
        return Ok(new { accepted = true });
    }

    [HttpPost("decline/{userId:guid}")]
    public async Task<IActionResult> Decline(Guid userId)
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        var fr = await _db.Friendships.SingleOrDefaultAsync(f =>
            f.RequesterId == userId && f.AddresseeId == me && f.Status == FriendshipStatus.Pending);

        if (fr == null) return NotFound();

        _db.Friendships.Remove(fr);
        await _db.SaveChangesAsync();
        return Ok(new { declined = true });
    }

    [HttpGet("requests/incoming")]
    public async Task<ActionResult<IEnumerable<FriendRequestDto>>> Incoming()
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        var list = await _db.Friendships
            .Where(f => f.Status == FriendshipStatus.Pending && f.AddresseeId == me)
            .Join(_db.Users, f => f.RequesterId, u => u.Id, (f, u) =>
                new FriendRequestDto(u.Id.ToString(), u.UserName, u.Email, f.CreatedAt))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("requests/outgoing")]
    public async Task<ActionResult<IEnumerable<FriendRequestDto>>> Outgoing()
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        var list = await _db.Friendships
            .Where(f => f.Status == FriendshipStatus.Pending && f.RequesterId == me)
            .Join(_db.Users, f => f.AddresseeId, u => u.Id, (f, u) =>
                new FriendRequestDto(u.Id.ToString(), u.UserName, u.Email, f.CreatedAt))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(list);
    }
}
