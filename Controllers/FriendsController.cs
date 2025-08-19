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
    private readonly AppDbContext db;
    public FriendsController(AppDbContext db) => this.db = db;

    private bool TryGetUserId(out Guid userId)
    {
        var idStr = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idStr, out userId);
    }

    [HttpGet("list")]
    public async Task<ActionResult<IEnumerable<FriendDto>>> List()
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        var friendIds = await db.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                        (f.RequesterId == me || f.AddresseeId == me))
            .Select(f => f.RequesterId == me ? f.AddresseeId : f.RequesterId)
            .Distinct()
            .ToListAsync();

        if (friendIds.Count == 0) return Ok(Array.Empty<FriendDto>());

        var users = await db.Users
            .Where(u => friendIds.Contains(u.Id))
            .OrderBy(u => u.UserName)
            .Select(u => new FriendDto(u.Id, u.UserName, u.Email))
            .ToListAsync();

        return Ok(users);
    }

    // === НОВОЕ: отправить/склеить дружбу ===
    [HttpPost("request/{userId:guid}")]
    public async Task<IActionResult> SendRequest(Guid userId)
    {
        if (!TryGetUserId(out var me)) return Unauthorized();
        if (userId == me) return BadRequest("Нельзя добавить себя.");

        var targetExists = await db.Users.AnyAsync(u => u.Id == userId);
        if (!targetExists) return NotFound("Пользователь не найден.");

        var existing = await db.Friendships.SingleOrDefaultAsync(f =>
            (f.RequesterId == me && f.AddresseeId == userId) ||
            (f.RequesterId == userId && f.AddresseeId == me));

        if (existing != null)
        {
            if (existing.Status == FriendshipStatus.Accepted)
                return Ok(new { alreadyFriends = true });

            if (existing.Status == FriendshipStatus.Pending)
            {
                // Если встречная заявка уже есть — сразу принимаем
                if (existing.RequesterId == userId && existing.AddresseeId == me)
                {
                    existing.Status = FriendshipStatus.Accepted;
                    await db.SaveChangesAsync();
                    return Ok(new { accepted = true, matched = true });
                }
                // Иначе — заявка уже отправлена мной раньше
                return Ok(new { pending = true, alreadySent = true });
            }

            // На будущее: Blocked/Rejected и т.п.
        }

        db.Friendships.Add(new Friendship
        {
            Id = Guid.NewGuid(),
            RequesterId = me,
            AddresseeId = userId,
            Status = FriendshipStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return Ok(new { pending = true, created = true });
    }

    // (опционально, отдельно явное подтверждение)
    [HttpPost("accept/{userId:guid}")]
    public async Task<IActionResult> Accept(Guid userId)
    {
        if (!TryGetUserId(out var me)) return Unauthorized();

        var fr = await db.Friendships.SingleOrDefaultAsync(f =>
            f.RequesterId == userId && f.AddresseeId == me && f.Status == FriendshipStatus.Pending);

        if (fr == null) return NotFound();
        fr.Status = FriendshipStatus.Accepted;
        await db.SaveChangesAsync();
        return Ok(new { accepted = true });
    }
}
