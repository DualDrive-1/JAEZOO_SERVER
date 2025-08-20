using System.Text.Json;
using BCryptNet = BCrypt.Net.BCrypt;
using JaeZoo.Server.Data;
using JaeZoo.Server.Models;
using JaeZoo.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JaeZoo.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;

    public AuthController(AppDbContext db, TokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] JsonElement body)
    {
        string? userName = GetString(body, "UserName", "Username", "login", "name");
        string? email = GetString(body, "Email", "email");
        string? password = GetString(body, "Password", "password", "pass");

        if (string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            return BadRequest("UserName, Email, Password are required.");
        }

        var exists = await _db.Users.AnyAsync(u => u.UserName == userName || u.Email == email);
        if (exists) return Conflict("User with same username or email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            PasswordHash = BCryptNet.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokens.IssueJwt(user);

        // Возвращаем анонимный объект — клиенту пофиг, а нам не нужен конкретный TokenResponse
        return Ok(new
        {
            token,
            userId = user.Id.ToString(),
            userName = user.UserName ?? string.Empty
        });
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] JsonElement body)
    {
        // Поддерживаем любые варианты поля логина
        string? login = GetString(body, "Login", "LoginOrEmail", "login", "Username", "UserName", "Email", "email");
        string? password = GetString(body, "Password", "password", "pass");

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            return BadRequest("Login and Password are required.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserName == login || u.Email == login);
        if (user == null || !BCryptNet.Verify(password, user.PasswordHash))
            return Unauthorized("Invalid credentials.");

        var token = _tokens.IssueJwt(user);

        return Ok(new
        {
            token,
            userId = user.Id.ToString(),
            userName = user.UserName ?? string.Empty
        });
    }

    private static string? GetString(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (body.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        }
        return null;
    }
}
