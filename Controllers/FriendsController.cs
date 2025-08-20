using JaeZoo.Server.Data;
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

    public FriendsController(AppDbContext db)
    {
        _db = db;
    }

    // ======== DTOs, только для сервера ========
    public record FriendDto(Guid UserId, string UserName, string? Email);
    public record FriendRequestDto(Guid RequestId, Guid UserId, string UserName, string? Email, DateTime CreatedAt);
    public record FriendRequestsResponse(List<FriendRequestDto> Incoming, List<FriendRequestDto> Outgoing);

    // извлекаем текущего пользователя из клейма
    private bool TryGetUserId(out Guid userId)
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(s, out userId);
    }

    // ======== Список друзей (два маршрута для совместимости) ========
    [HttpGet]
    [HttpGet("my")]
    public async Task<ActionResult<List<FriendDto>>> GetMyFriends()
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        // я -> он (accepted)
        var q1 =
            from f in _db.Friendships
            where f.Status == FriendshipStatus.Accepted && f.RequesterId == me
            join u in _db.Users on f.AddresseeId equals u.Id
            select new FriendDto(u.Id, u.UserName, u.Email);

        // он -> я (accepted)
        var q2 =
            from f in _db.Friendships
            where f.Status == FriendshipStatus.Accepted && f.AddresseeId == me
            join u in _db.Users on f.RequesterId equals u.Id
            select new FriendDto(u.Id, u.UserName, u.Email);

        var list = await q1.Union(q2).Distinct().ToListAsync();
        return Ok(list);
    }

    // ======== Входящие/исходящие заявки ========
    [HttpGet("requests")]
    public async Task<ActionResult<FriendRequestsResponse>> GetRequests()
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        var incoming =
            await (from f in _db.Friendships
                   where f.Status == FriendshipStatus.Pending && f.AddresseeId == me
                   join u in _db.Users on f.RequesterId equals u.Id
                   orderby f.CreatedAt descending
                   select new FriendRequestDto(f.Id, u.Id, u.UserName, u.Email, f.CreatedAt))
                  .ToListAsync();

        var outgoing =
            await (from f in _db.Friendships
                   where f.Status == FriendshipStatus.Pending && f.RequesterId == me
                   join u in _db.Users on f.AddresseeId equals u.Id
                   orderby f.CreatedAt descending
                   select new FriendRequestDto(f.Id, u.Id, u.UserName, u.Email, f.CreatedAt))
                  .ToListAsync();

        return Ok(new FriendRequestsResponse(incoming, outgoing));
    }

    // ======== Отправить заявку ========
    [HttpPost("request/{toUserId:guid}")]
    public async Task<IActionResult> SendRequest(Guid toUserId)
    {
        if (!TryGetUserId(out var me)) return Unauthorized();
        if (me == toUserId) return BadRequest("Нельзя добавить в друзья самого себя.");

        // нет ли уже заявки/дружбы в любом направлении
        var exists = await _db.Friendships.AnyAsync(f =>
            (f.RequesterId == me && f.AddresseeId == toUserId) ||
            (f.RequesterId == toUserId && f.AddresseeId == me));

        if (exists) return Conflict("Заявка уже существует или вы уже друзья.");

        var fr = new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = me,
            AddresseeId = toUserId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Friendships.Add(fr);
        await _db.SaveChangesAsync();
        return Ok(new { requested = true, requestId = fr.Id });
    }

    // ======== Принять заявку (по пользователю-отправителю) ========
    [HttpPost("accept/{userId:guid}")]
    public async Task<IActionResult> Accept(Guid userId)
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        var fr = await _db.Friendships.SingleOrDefaultAsync(f =>
            f.RequesterId == userId &&
            f.AddresseeId == me &&
            f.Status == FriendshipStatus.Pending);

        if (fr == null) return NotFound("Заявка не найдена.");
        fr.Status = FriendshipStatus.Accepted;
        await _db.SaveChangesAsync();
        return Ok(new { accepted = true });
    }

    // ======== Отклонить заявку (по пользователю-отправителю) ========
    [HttpPost("reject/{userId:guid}")]
    public async Task<IActionResult> Reject(Guid userId)
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        var fr = await _db.Friendships.SingleOrDefaultAsync(f =>
            f.RequesterId == userId &&
            f.AddresseeId == me &&
            f.Status == FriendshipStatus.Pending);

        if (fr == null) return NotFound("Заявка не найдена.");
        _db.Friendships.Remove(fr);
        await _db.SaveChangesAsync();
        return Ok(new { rejected = true });
    }

    // ======== Удалить из друзей (в любую сторону) ========
    [HttpDelete("remove/{userId:guid}")]
    public async Task<IActionResult> Remove(Guid userId)
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        var fr = await _db.Friendships.SingleOrDefaultAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            ((f.RequesterId == me && f.AddresseeId == userId) ||
             (f.RequesterId == userId && f.AddresseeId == me)));

        if (fr == null) return NotFound("Запись о дружбе не найдена.");
        _db.Friendships.Remove(fr);
        await _db.SaveChangesAsync();
        return Ok(new { removed = true });
    }
}
