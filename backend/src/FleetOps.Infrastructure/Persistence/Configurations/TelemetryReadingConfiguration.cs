using FleetOps.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetOps.Infrastructure.Persistence.Configurations;

public sealed class TelemetryReadingConfiguration : IEntityTypeConfiguration<TelemetryReading>
{
    public void Configure(EntityTypeBuilder<TelemetryReading> builder)
    {
        builder.ToTable("telemetry_readings");
        builder.HasKey(t => t.Id);

        // The dashboard's hot path is "latest N readings for one vessel".
        builder.HasIndex(t => new { t.VesselId, t.RecordedAtUtc })
            .HasDatabaseName("ix_telemetry_vessel_recorded_at")
            .IsDescending(false, true);

        builder.Ignore(t => t.FuelPerNauticalMile);
    }
}
