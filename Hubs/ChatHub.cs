using JaeZoo.Server.Data;
using JaeZoo.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace JaeZoo.Server.Hubs;

[Authorize]
public class ChatHub(AppDbContext db) : Hub
{
    private Guid MeId => Guid.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static (Guid a, Guid b) OrderPair(Guid x, Guid y) => x < y ? (x, y) : (y, x);

    private async Task<bool> AreFriends(Guid me, Guid other) =>
        await db.Friendships.AnyAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            ((f.RequesterId == me && f.AddresseeId == other) ||
             (f.RequesterId == other && f.AddresseeId == me)));

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

    public async Task SendDirectMessage(Guid targetUserId, string text)
    {
        text = (text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        var me = MeId;

        if (!await AreFriends(me, targetUserId))
            throw new HubException("Вы не друзья.");

        var dlg = await GetOrCreateDialog(me, targetUserId);

        var msg = new DirectMessage
        {
            DialogId = dlg.Id,
            SenderId = me,
            Text = text,
            SentAt = DateTime.UtcNow
        };
        db.DirectMessages.Add(msg);
        await db.SaveChangesAsync();

        // Отправляем обоим юзерам
        var payload = new { senderId = msg.SenderId, text = msg.Text, sentAt = msg.SentAt };
        await Clients.Users(me.ToString(), targetUserId.ToString())
            .SendAsync("ReceiveDirectMessage", payload);
    }
}
