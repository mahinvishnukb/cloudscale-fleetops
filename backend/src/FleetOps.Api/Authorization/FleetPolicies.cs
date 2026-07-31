using FleetOps.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace FleetOps.Api.Authorization;

/// <summary>
/// Named authorization policies. Controllers reference these constants rather than
/// role strings, so a role rename is a single-file change.
/// </summary>
public static class FleetPolicies
{
    /// <summary>Read-only access to fleet data. Every authenticated role qualifies.</summary>
    public const string ReadFleet = nameof(ReadFleet);

    /// <summary>Mutating fleet operations: register vessels, change status, acknowledge anomalies.</summary>
    public const string ManageFleet = nameof(ManageFleet);

    /// <summary>Destructive or tenant-wide operations.</summary>
    public const string Administer = nameof(Administer);

    public static AuthorizationOptions AddFleetPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(ReadFleet, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(FleetRoles.Administrator, FleetRoles.FleetManager, FleetRoles.Analyst));

        options.AddPolicy(ManageFleet, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(FleetRoles.Administrator, FleetRoles.FleetManager));

        options.AddPolicy(Administer, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(FleetRoles.Administrator));

        return options;
    }
}
