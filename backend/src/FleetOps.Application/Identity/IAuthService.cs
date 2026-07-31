namespace FleetOps.Application.Identity;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}
