using FleetOps.Application.Abstractions;
using FleetOps.Domain.Identity;
using FleetOps.Domain.Vessels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetOps.Infrastructure.Persistence;

/// <summary>
/// Seeds a demo fleet and demo accounts on an empty database so the dashboard has
/// something to show on first run. Idempotent: safe to call on every startup.
/// </summary>
public sealed class DatabaseSeeder(
    FleetOpsDbContext db,
    IPasswordHasher passwordHasher,
    ILogger<DatabaseSeeder> logger)
{
    public sealed record DemoVessel(string Imo, string Name, VesselType Type, string Port, int Tonnage);

    /// <summary>
    /// The demo fleet. Every IMO here must pass its check digit, or constructing the
    /// <see cref="Vessel"/> throws and the whole seed is abandoned — which is exactly
    /// what happened the first time this ran. Public so DemoFleetTests can assert it.
    /// </summary>
    public static readonly IReadOnlyList<DemoVessel> DemoFleet =
    [
        new("9074729", "MV Northern Aurora",  VesselType.ContainerShip, "Vancouver, CA",     92_500),
        new("9395044", "MV Cascadia Trader",  VesselType.BulkCarrier,   "Halifax, CA",       58_200),
        new("9321483", "MV Saint Lawrence",   VesselType.Tanker,        "Montreal, CA",      74_100),
        new("9186479", "MV Pacific Sentinel", VesselType.ContainerShip, "Prince Rupert, CA", 110_300),
        new("9247388", "MV Acadian Star",     VesselType.RoRo,          "Saint John, CA",    31_900),
        new("9465124", "MV Beaufort Pioneer", VesselType.Reefer,        "Churchill, CA",     22_400),
    ];

    public async Task SeedAsync(string demoPassword, CancellationToken ct = default)
    {
        await SeedUsersAsync(demoPassword, ct);
        await SeedVesselsAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedUsersAsync(string demoPassword, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        var hash = passwordHasher.Hash(demoPassword);

        db.Users.AddRange(
            new AppUser("admin", "admin@fleetops.local", hash, FleetRoles.Administrator),
            new AppUser("manager", "manager@fleetops.local", hash, FleetRoles.FleetManager),
            new AppUser("analyst", "analyst@fleetops.local", hash, FleetRoles.Analyst));

        logger.LogInformation("Seeded 3 demo users (admin / manager / analyst)");
    }

    private async Task SeedVesselsAsync(CancellationToken ct)
    {
        if (await db.Vessels.AnyAsync(ct))
        {
            return;
        }

        // Spread the fleet across statuses so the dashboard tiles and the simulator
        // both have something interesting to show on first run.
        var statuses = new[]
        {
            VesselStatus.UnderWay, VesselStatus.UnderWay, VesselStatus.UnderWay,
            VesselStatus.InPort, VesselStatus.AtAnchor, VesselStatus.Maintenance,
        };

        for (var i = 0; i < DemoFleet.Count; i++)
        {
            var demo = DemoFleet[i];

            var vessel = new Vessel(
                ImoNumber.Create(demo.Imo), demo.Name, demo.Type, demo.Port, demo.Tonnage);

            vessel.ChangeStatus(statuses[i % statuses.Length]);
            db.Vessels.Add(vessel);
        }

        logger.LogInformation("Seeded {Count} demo vessels", DemoFleet.Count);
    }
}
