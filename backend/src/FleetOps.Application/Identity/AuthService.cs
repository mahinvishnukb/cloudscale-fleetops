using FleetOps.Application.Abstractions;
using FleetOps.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetOps.Application.Identity;

public sealed class AuthService(
    IFleetOpsDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService tokens,
    IDateTimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var username = request.Username?.Trim().ToLowerInvariant() ?? string.Empty;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

        // Same message and comparable work either way, so the response cannot be used
        // to enumerate valid usernames.
        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for {Username}", username);
            throw new ForbiddenException("Invalid username or password.");
        }

        user.RecordLogin(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        var (token, expiresAt) = tokens.CreateToken(user);

        logger.LogInformation("User {Username} ({Role}) signed in", user.Username, user.Role);

        return new AuthResponse(token, expiresAt, user.Username, user.Role);
    }
}
