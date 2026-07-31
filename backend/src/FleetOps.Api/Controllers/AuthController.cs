using FleetOps.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FleetOps.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Exchanges credentials for a signed JWT.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> LoginAsync(
        [FromBody] LoginRequest request, CancellationToken ct)
        => Ok(await auth.LoginAsync(request, ct));

    /// <summary>Returns the claims on the caller's current token. Useful for debugging RBAC.</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<object> Me() => Ok(new
    {
        Username = User.Identity?.Name,
        Roles = User.Claims
            .Where(c => c.Type.EndsWith("/role", StringComparison.Ordinal) || c.Type == "role")
            .Select(c => c.Value),
        IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
    });
}
