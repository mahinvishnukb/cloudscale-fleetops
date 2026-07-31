namespace FleetOps.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "fleetops-api";

    public string Audience { get; set; } = "fleetops-ui";

    public int LifetimeMinutes { get; set; } = 60;
}
