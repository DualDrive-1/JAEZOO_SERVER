using JaeZoo.Server.Data;
using JaeZoo.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace JaeZoo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // <--- закрыли контроллер
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
        if (!TryGetUserId(out var me)) return Unauthorized(); // <--- больше не NRE

        var friendIds = await db.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                        (f.RequesterId == me || f.AddresseeId == me))
            .Select(f => f.RequesterId == me ? f.AddresseeId : f.RequesterId)
            .Distinct()
            .ToListAsync();

        if (friendIds.Count == 0)
            return Ok(Array.Empty<FriendDto>());

        var users = await db.Users
            .Where(u => friendIds.Contains(u.Id))
            .OrderBy(u => u.UserName)
            .Select(u => new FriendDto(u.Id, u.UserName, u.Email))
            .ToListAsync();

        return Ok(users);
    }
}
