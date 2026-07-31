namespace FleetOps.Domain.Identity;

/// <summary>Role names used by the RBAC policies. Kept in the domain so they cannot drift.</summary>
public static class FleetRoles
{
    public const string Administrator = "Administrator";
    public const string FleetManager = "FleetManager";
    public const string Analyst = "Analyst";

    public static readonly IReadOnlyList<string> All = [Administrator, FleetManager, Analyst];
}
