using FleetOps.Domain.Identity;
using FleetOps.Domain.Manifests;
using FleetOps.Domain.Telemetry;
using FleetOps.Domain.Vessels;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Application.Abstractions;

/// <summary>
/// Persistence seam. The Application layer depends on this abstraction rather than
/// the concrete DbContext, so use cases can be tested against the in-memory provider.
/// </summary>
public interface IFleetOpsDbContext
{
    DbSet<Vessel> Vessels { get; }

    DbSet<TelemetryReading> TelemetryReadings { get; }

    DbSet<Anomaly> Anomalies { get; }

    DbSet<CargoManifest> CargoManifests { get; }

    DbSet<CargoLineItem> CargoLineItems { get; }

    DbSet<AppUser> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
