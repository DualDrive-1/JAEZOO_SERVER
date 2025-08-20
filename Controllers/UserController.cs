using JaeZoo.Server.Data;
using JaeZoo.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using JaeZoo.Server.Dtos;                 // ✅ наши серверные DTO

namespace JaeZoo.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // контроллер доступен только с JWT
public class UsersController : ControllerBase
{
    private readonly AppDbContext db;
    public UsersController(AppDbContext db) => this.db = db;

    private bool TryGetUserId(out Guid userId)
    {
        var idStr = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idStr, out userId);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<UserSearchDto>>> Search([FromQuery] string q)
    {
        if (!TryGetUserId(out var meId)) return Unauthorized();

        var query = (q ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(query))
            return Ok(Array.Empty<UserSearchDto>());

        var qLower = query.ToLower();

        var items = await db.Users
            .Where(u => u.Id != meId &&
                        (u.UserName.ToLower().Contains(qLower) ||
                         u.Email.ToLower().Contains(qLower)))
            .OrderBy(u => u.UserName)
            .Take(20)
            // ✅ string Id для DTO
            .Select(u => new UserSearchDto(u.Id.ToString(), u.UserName, u.Email))
            .ToListAsync();

        return Ok(items);
    }
}
