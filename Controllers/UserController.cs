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
public class UserController : ControllerBase
{
    private readonly AppDbContext _db;
    public UserController(AppDbContext db) => _db = db;

    // GET /api/user/search?query=...
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<UserSearchDto>>> Search([FromQuery] string? query)
    {
        query = (query ?? "").Trim();

        var q = _db.Users.AsNoTracking();

        if (!string.IsNullOrEmpty(query))
        {
            q = q.Where(u =>
                u.UserName.ToLower().Contains(query.ToLower()) ||
                u.Email.ToLower().Contains(query.ToLower()));
        }

        var data = await q
            .OrderBy(u => u.UserName)
            .Take(50)
            .Select(u => new UserSearchDto(u.Id, u.UserName, u.Email))
            .ToListAsync();

        return Ok(data);
    }
}
