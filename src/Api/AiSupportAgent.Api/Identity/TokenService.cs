using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AiSupportAgent.Api.Identity;

public class TokenService(IOptions<JwtOptions> opts, IConfiguration config)
{
    private readonly JwtOptions _opts = opts.Value;
    private readonly string? _demoEmail = config["Auth:DemoEmail"];

    public (string token, DateTime expiresAtUtc) CreateAccessToken(ApplicationUser user, Guid tenantId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_opts.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new("sub", user.Id),
            new("email", user.Email ?? string.Empty),
            new("tenantId", tenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrEmpty(_demoEmail)
            && string.Equals(user.Email, _demoEmail, StringComparison.OrdinalIgnoreCase))
            claims.Add(new Claim("is_demo", "true"));

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer, audience: _opts.Audience,
            claims: claims, expires: expires, signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public (string raw, string hash, DateTime expiresAtUtc) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var raw = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return (raw, Hash(raw), DateTime.UtcNow.AddDays(_opts.RefreshTokenDays));
    }

    public static string Hash(string token) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

}