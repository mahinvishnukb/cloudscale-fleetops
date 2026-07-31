using System.Reflection;
using FleetOps.Application.Abstractions;
using FleetOps.Domain.Identity;
using FleetOps.Domain.Manifests;
using FleetOps.Domain.Telemetry;
using FleetOps.Domain.Vessels;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.Infrastructure.Persistence;

public sealed class FleetOpsDbContext(DbContextOptions<FleetOpsDbContext> options)
    : DbContext(options), IFleetOpsDbContext
{
    public DbSet<Vessel> Vessels => Set<Vessel>();

    public DbSet<TelemetryReading> TelemetryReadings => Set<TelemetryReading>();

    public DbSet<Anomaly> Anomalies => Set<Anomaly>();

    public DbSet<CargoManifest> CargoManifests => Set<CargoManifest>();

    public DbSet<CargoLineItem> CargoLineItems => Set<CargoLineItem>();

    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
