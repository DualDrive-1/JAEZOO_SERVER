using JaeZoo.Server.Data;
using JaeZoo.Server.Dtos;
using JaeZoo.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace JaeZoo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly AppDbContext _db;
    public FriendsController(AppDbContext db) => _db = db;

    private Guid CurrentUserId()
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/friends/list
    [HttpGet("list")]
    public async Task<ActionResult<IEnumerable<FriendDto>>> List()
    {
        var me = CurrentUserId();

        var q = _db.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterId == me || f.AddresseeId == me))
            .Select(f => new
            {
                Status = f.Status,
                OtherId = f.RequesterId == me ? f.AddresseeId : f.RequesterId
            })
            .Join(_db.Users, x => x.OtherId, u => u.Id, (x, u) => new FriendDto(
                u.Id,
                u.UserName,
                u.Email,
                FriendshipStatusDto.Accepted
            ));

        var data = await q.ToListAsync();
        return Ok(data);
    }

    // GET /api/friends/requests/incoming
    [HttpGet("requests/incoming")]
    public async Task<ActionResult<IEnumerable<FriendDto>>> Incoming()
    {
        var me = CurrentUserId();

        var data = await _db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Pending && f.AddresseeId == me)
            .Join(_db.Users, f => f.RequesterId, u => u.Id, (f, u) => new FriendDto(
                u.Id, u.UserName, u.Email, FriendshipStatusDto.Pending))
            .ToListAsync();

        return Ok(data);
    }

    // GET /api/friends/requests/outgoing
    [HttpGet("requests/outgoing")]
    public async Task<ActionResult<IEnumerable<FriendDto>>> Outgoing()
    {
        var me = CurrentUserId();

        var data = await _db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Pending && f.RequesterId == me)
            .Join(_db.Users, f => f.AddresseeId, u => u.Id, (f, u) => new FriendDto(
                u.Id, u.UserName, u.Email, FriendshipStatusDto.Pending))
            .ToListAsync();

        return Ok(data);
    }

    // POST /api/friends/request/{userId}
    [HttpPost("request/{userId}")]
    public async Task<IActionResult> Request(Guid userId)
    {
        var me = CurrentUserId();
        if (me == userId) return BadRequest("You cannot add yourself.");

        var other = await _db.Users.FindAsync(userId);
        if (other is null) return NotFound("User not found.");

        var existing = await _db.Friendships
            .FirstOrDefaultAsync(f =>
                (f.RequesterId == me && f.AddresseeId == userId) ||
                (f.RequesterId == userId && f.AddresseeId == me));

        if (existing is not null)
        {
            if (existing.Status == FriendshipStatus.Accepted)
                return Conflict("Already friends.");

            // Если встречная заявка — сразу принимаем
            if (existing.Status == FriendshipStatus.Pending && existing.RequesterId == userId)
            {
                existing.Status = FriendshipStatus.Accepted;
                await _db.SaveChangesAsync();
                return Ok();
            }

            // Иначе дубликат
            return Conflict("Request already exists.");
        }

        _db.Friendships.Add(new Friendship
        {
            RequesterId = me,
            AddresseeId = userId,
            Status = FriendshipStatus.Pending
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    // POST /api/friends/accept/{userId}
    [HttpPost("accept/{userId}")]
    public async Task<IActionResult> Accept(Guid userId)
    {
        var me = CurrentUserId();

        var f = await _db.Friendships.FirstOrDefaultAsync(x =>
            x.RequesterId == userId && x.AddresseeId == me && x.Status == FriendshipStatus.Pending);

        if (f is null) return NotFound("Request not found.");
        f.Status = FriendshipStatus.Accepted;
        await _db.SaveChangesAsync();
        return Ok();
    }

    // POST /api/friends/cancel/{userId} — отмена своей исходящей заявки или удаление дружбы
    [HttpPost("cancel/{userId}")]
    public async Task<IActionResult> Cancel(Guid userId)
    {
        var me = CurrentUserId();

        var f = await _db.Friendships.FirstOrDefaultAsync(x =>
            (x.RequesterId == me && x.AddresseeId == userId) ||
            (x.RequesterId == userId && x.AddresseeId == me));

        if (f is null) return NotFound();

        // если Pending и я отправитель — просто удалим
        if (f.Status == FriendshipStatus.Pending && f.RequesterId == me)
        {
            _db.Friendships.Remove(f);
        }
        else
        {
            // разрываем дружбу
            _db.Friendships.Remove(f);
        }

        await _db.SaveChangesAsync();
        return Ok();
    }
}
