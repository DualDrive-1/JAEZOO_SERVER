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
public class ChatController : ControllerBase
{
    private readonly AppDbContext _db;
    public ChatController(AppDbContext db) => _db = db;

    private Guid MeId
    {
        get
        {
            var idStr = User.FindFirst("sub")?.Value
                        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
        }
    }

    private static (Guid a, Guid b) OrderPair(Guid x, Guid y) => x < y ? (x, y) : (y, x);

    private Task<DirectDialog?> GetDialog(Guid u1, Guid u2)
    {
        var (a, b) = OrderPair(u1, u2);
        return _db.DirectDialogs.FirstOrDefaultAsync(d => d.User1Id == a && d.User2Id == b);
    }

    private Task<bool> AreFriends(Guid me, Guid other) =>
        _db.Friendships.AnyAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            ((f.RequesterId == me && f.AddresseeId == other) ||
             (f.RequesterId == other && f.AddresseeId == me)));

    [HttpGet("history/{friendId:guid}")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> History(Guid friendId, int skip = 0, int take = 50)
    {
        if (MeId == Guid.Empty) return Unauthorized();

        // Не друзья — пустая история (чтобы клиент не падал)
        if (!await AreFriends(MeId, friendId))
            return Ok(Array.Empty<MessageDto>());

        var dlg = await GetDialog(MeId, friendId);
        if (dlg is null) return Ok(Array.Empty<MessageDto>());

        var items = await _db.DirectMessages
            .Where(m => m.DialogId == dlg.Id)
            .OrderBy(m => m.SentAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .Select(m => new MessageDto(
                m.SenderId.ToString(),
                m.Text,
                m.SentAt))
            .ToListAsync();

        return Ok(items);
    }
}
