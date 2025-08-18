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
        var meId = Guid.Parse(User.FindFirst("sub")!.Value);
        var query = (q ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(query))
            return Ok(Array.Empty<UserSearchDto>());

        // простой регистронезависимый поиск по никнейму/почте
        var qLower = query.ToLower();

        var items = await db.Users
            .Where(u => u.Id != meId &&
                        (u.UserName.ToLower().Contains(qLower) ||
                         u.Email.ToLower().Contains(qLower)))
            .OrderBy(u => u.UserName)
            .Take(20)
            .Select(u => new UserSearchDto(u.Id, u.UserName, u.Email))
            .ToListAsync();

        return Ok(items);
    }

}
