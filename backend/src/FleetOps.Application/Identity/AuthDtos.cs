namespace FleetOps.Application.Identity;

public sealed record LoginRequest(string Username, string Password);

public sealed record AuthResponse(
    string Token,
    DateTime ExpiresAtUtc,
    string Username,
    string Role);
