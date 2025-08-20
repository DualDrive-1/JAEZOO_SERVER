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
public class ChatController : ControllerBase
{
    private readonly AppDbContext _db;
    public ChatController(AppDbContext db) => _db = db;

    private Guid Me() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/chat/history/{otherId}
    [HttpGet("history/{otherId:guid}")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> History(Guid otherId)
    {
        var me = Me();
        var (a, b) = me.CompareTo(otherId) < 0 ? (me, otherId) : (otherId, me);

        var dialog = await _db.DirectDialogs.FirstOrDefaultAsync(d => d.User1Id == a && d.User2Id == b);
        if (dialog is null) return Ok(Array.Empty<MessageDto>());

        var data = await _db.DirectMessages.AsNoTracking()
            .Where(m => m.DialogId == dialog.Id)
            .OrderBy(m => m.SentAt)
            .Select(m => new MessageDto(m.SenderId, m.Text, m.SentAt))
            .ToListAsync();

        return Ok(data);
    }

    // POST /api/chat/send
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest req)
    {
        var me = Me();
        if (me == req.RecipientId) return BadRequest("Cannot send to yourself.");

        var (a, b) = me.CompareTo(req.RecipientId) < 0 ? (me, req.RecipientId) : (req.RecipientId, me);

        var dialog = await _db.DirectDialogs.FirstOrDefaultAsync(d => d.User1Id == a && d.User2Id == b);
        if (dialog is null)
        {
            dialog = new DirectDialog { User1Id = a, User2Id = b };
            _db.DirectDialogs.Add(dialog);
            await _db.SaveChangesAsync();
        }

        _db.DirectMessages.Add(new DirectMessage
        {
            DialogId = dialog.Id,
            SenderId = me,
            Text = req.Text
        });
        await _db.SaveChangesAsync();
        return Ok();
    }
}
