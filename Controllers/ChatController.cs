using JaeZoo.Server.Data;
using JaeZoo.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JaeZoo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController(AppDbContext db) : ControllerBase
{
    private Guid MeId => Guid.Parse(User.FindFirst("sub")!.Value);

    private static (Guid a, Guid b) OrderPair(Guid x, Guid y) => x < y ? (x, y) : (y, x);

    private async Task<DirectDialog> GetOrCreateDialog(Guid aId, Guid bId)
    {
        var (u1, u2) = OrderPair(aId, bId);
        var dlg = await db.DirectDialogs.FirstOrDefaultAsync(d => d.User1Id == u1 && d.User2Id == u2);
        if (dlg is not null) return dlg;

        dlg = new DirectDialog { User1Id = u1, User2Id = u2 };
        db.DirectDialogs.Add(dlg);
        await db.SaveChangesAsync();
        return dlg;
    }

    private Task<bool> AreFriends(Guid me, Guid other) =>
        db.Friendships.AnyAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            ((f.RequesterId == me && f.AddresseeId == other) ||
             (f.RequesterId == other && f.AddresseeId == me)));

    [HttpGet("history/{friendId:guid}")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> History(Guid friendId, int skip = 0, int take = 50)
    {
        if (!await AreFriends(MeId, friendId)) return Forbid();

        var dlg = await GetOrCreateDialog(MeId, friendId);

        var items = await db.DirectMessages
            .Where(m => m.DialogId == dlg.Id)
            .OrderBy(m => m.SentAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .Select(m => new MessageDto(m.SenderId, m.Text, m.SentAt))
            .ToListAsync();

        return Ok(items);
    }
}
