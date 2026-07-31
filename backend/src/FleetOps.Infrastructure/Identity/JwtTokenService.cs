using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FleetOps.Application.Abstractions;
using FleetOps.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FleetOps.Infrastructure.Identity;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider clock) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateToken(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(_options.Key) || Encoding.UTF8.GetByteCount(_options.Key) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key must be configured and at least 32 bytes. Generate one with: openssl rand -base64 48");
        }

        var expiresAt = clock.UtcNow.AddMinutes(_options.LifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: clock.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
