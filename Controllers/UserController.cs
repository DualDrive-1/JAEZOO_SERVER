using JaeZoo.Server.Data;
using JaeZoo.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JaeZoo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(AppDbContext db) : ControllerBase
{
    [Authorize]
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<UserSearchDto>>> Search([FromQuery] string q)
    {
        var me = User.FindFirst("sub")!.Value;
        var query = (q ?? "").Trim().ToLower();
        if (string.IsNullOrWhiteSpace(query)) return Ok(Array.Empty<UserSearchDto>());

        var items = await db.Users
            .Where(u => u.Id.ToString() != me &&
                        (EF.Functions.ILike(u.UserName, $"%{query}%") ||
                         EF.Functions.ILike(u.Email, $"%{query}%")))
            .OrderBy(u => u.UserName)
            .Take(20)
            .Select(u => new UserSearchDto(u.Id, u.UserName, u.Email))
            .ToListAsync();

        return Ok(items);
    }
}
