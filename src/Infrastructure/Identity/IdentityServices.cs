using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;

namespace TaskFlow.Infrastructure.Identity;

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;
    public JwtService(IConfiguration config) => _config = config;

    public string GenerateAccessToken(int userId, int tenantId, string email, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("tenantId", tenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenExpiryMinutes"] ?? "60")),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public CurrentUser? ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!);
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true, ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = true, ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            }, out var validated);
            var jwt = (JwtSecurityToken)validated;
            return new CurrentUser(
                int.Parse(jwt.Subject),
                int.Parse(jwt.Claims.First(c => c.Type == "tenantId").Value),
                jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value,
                jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value
            );
        }
        catch { return null; }
    }
}

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}

public class CurrentUserService : ICurrentUserService
{
    private CurrentUser? _user;
    public CurrentUser? User => _user;
    public bool IsAuthenticated => _user is not null;
    public void SetUser(CurrentUser user) => _user = user;
}
