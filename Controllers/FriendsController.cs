using JaeZoo.Server.Data;
using JaeZoo.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JaeZoo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendsController(AppDbContext db) : ControllerBase
{
    private Guid MeId => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet("list")]
    public async Task<ActionResult<IEnumerable<FriendDto>>> List()
    {
        var me = MeId;

        // Ищем айдишники друзей со статусом Accepted
        var friendIds = await db.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                        (f.RequesterId == me || f.AddresseeId == me))
            .Select(f => f.RequesterId == me ? f.AddresseeId : f.RequesterId)
            .Distinct()
            .ToListAsync();

        if (friendIds.Count == 0)
            return Ok(Array.Empty<FriendDto>()); // <-- ключевое: отдаём пустой список

        var users = await db.Users
            .Where(u => friendIds.Contains(u.Id))
            .OrderBy(u => u.UserName)
            .Select(u => new FriendDto(u.Id, u.UserName, u.Email))
            .ToListAsync();

        return Ok(users);
    }


    [HttpPost("request/{targetId:guid}")]
    public async Task<IActionResult> Request(Guid targetId)
    {
        var me = MeId;
        if (targetId == me) return BadRequest("Нельзя добавить себя.");

        // уже есть в одном из направлений?
        var exists = await db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == me && f.AddresseeId == targetId) ||
            (f.RequesterId == targetId && f.AddresseeId == me));

        if (exists is not null)
        {
            if (exists.Status == FriendshipStatus.Accepted) return Conflict("Вы уже друзья.");
            if (exists.RequesterId == me && exists.Status == FriendshipStatus.Pending) return Conflict("Заявка уже отправлена.");
            if (exists.RequesterId == targetId && exists.Status == FriendshipStatus.Pending) return Conflict("У вас есть входящая заявка.");
        }

        db.Friendships.Add(new Friendship { RequesterId = me, AddresseeId = targetId });
        await db.SaveChangesAsync();
        return Ok(new { message = "Заявка отправлена." });
    }

    [HttpPost("accept/{fromUserId:guid}")]
    public async Task<IActionResult> Accept(Guid fromUserId)
    {
        var me = MeId;
        var req = await db.Friendships.FirstOrDefaultAsync(f =>
            f.RequesterId == fromUserId && f.AddresseeId == me && f.Status == FriendshipStatus.Pending);

        if (req is null) return NotFound("Заявка не найдена.");
        req.Status = FriendshipStatus.Accepted;
        await db.SaveChangesAsync();
        return Ok(new { message = "Теперь вы друзья." });
    }
}
