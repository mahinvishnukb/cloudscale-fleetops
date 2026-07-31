using FleetOps.Domain.Common;

namespace FleetOps.Domain.Identity;

public sealed class AppUser : Entity
{
    private AppUser()
    {
        Username = string.Empty;
        Email = string.Empty;
        PasswordHash = string.Empty;
        Role = FleetRoles.Analyst;
    }

    public AppUser(string username, string email, string passwordHash, string role)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("Username is required.");
        }

        if (!FleetRoles.All.Contains(role))
        {
            throw new DomainException($"'{role}' is not a recognised role.");
        }

        Username = username.Trim().ToLowerInvariant();
        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
    }

    public string Username { get; private set; }

    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public string Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    public void RecordLogin(DateTime whenUtc)
    {
        LastLoginAtUtc = whenUtc;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }
}
