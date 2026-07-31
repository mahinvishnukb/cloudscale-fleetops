using FleetOps.Domain.Vessels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetOps.Infrastructure.Persistence.Configurations;

public sealed class VesselConfiguration : IEntityTypeConfiguration<Vessel>
{
    public void Configure(EntityTypeBuilder<Vessel> builder)
    {
        builder.ToTable("vessels");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.ImoNumber).HasMaxLength(7).IsRequired();
        builder.HasIndex(v => v.ImoNumber).IsUnique();

        // Nullable: only vessels discovered through AIS carry one, and it is not the
        // durable identity — a ship's MMSI changes when it re-flags.
        builder.Property(v => v.MmsiNumber).HasMaxLength(9);
        builder.HasIndex(v => v.MmsiNumber);

        builder.Property(v => v.Name).HasMaxLength(120).IsRequired();
        builder.Property(v => v.HomePort).HasMaxLength(120).IsRequired();

        // Stored as text so the database stays readable and enum reordering is non-breaking.
        builder.Property(v => v.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(v => v.Status);

        builder.HasMany(v => v.Telemetry)
            .WithOne()
            .HasForeignKey(t => t.VesselId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Vessel.Telemetry))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
