using System.Security.Claims;
using FleetOps.Application.Abstractions;

namespace FleetOps.Api.Services;

public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public string? Username => Principal?.FindFirstValue(ClaimTypes.Name);

    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
