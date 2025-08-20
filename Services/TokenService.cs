using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JaeZoo.Server.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JaeZoo.Server.Services
{
    public sealed class JwtOptions
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = "JaeZoo";
        public string Audience { get; set; } = "JaeZooClient";
        public int ExpiresMinutes { get; set; } = 60 * 24 * 7; // 7 дней
    }

    public class TokenService
    {
        private readonly JwtOptions _opt;

        public TokenService(IOptions<JwtOptions> options)
        {
            _opt = options.Value;
        }

        public string IssueJwt(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _opt.Issuer,
                audience: _opt.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_opt.ExpiresMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
