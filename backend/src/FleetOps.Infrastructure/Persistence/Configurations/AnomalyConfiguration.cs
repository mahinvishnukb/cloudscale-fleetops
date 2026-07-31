using FleetOps.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetOps.Infrastructure.Persistence.Configurations;

public sealed class AnomalyConfiguration : IEntityTypeConfiguration<Anomaly>
{
    public void Configure(EntityTypeBuilder<Anomaly> builder)
    {
        builder.ToTable("anomalies");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.Severity).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Detail).HasMaxLength(512).IsRequired();
        builder.Property(a => a.AcknowledgedBy).HasMaxLength(64);

        // Partial index: the alert panel only ever queries unacknowledged rows.
        builder.HasIndex(a => new { a.IsAcknowledged, a.DetectedAtUtc })
            .HasDatabaseName("ix_anomalies_open");

        builder.HasIndex(a => a.VesselId);
    }
}
