using FleetOps.Domain.Manifests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetOps.Infrastructure.Persistence.Configurations;

public sealed class CargoLineItemConfiguration : IEntityTypeConfiguration<CargoLineItem>
{
    public void Configure(EntityTypeBuilder<CargoLineItem> builder)
    {
        builder.ToTable("cargo_line_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ContainerNumber).HasMaxLength(11).IsRequired();
        builder.Property(i => i.Description).HasMaxLength(256).IsRequired();
        builder.Property(i => i.OriginPort).HasMaxLength(120).IsRequired();
        builder.Property(i => i.DestinationPort).HasMaxLength(120).IsRequired();
        builder.Property(i => i.HazardClass).HasMaxLength(16);

        builder.Property(i => i.GrossWeightKg).HasPrecision(10, 2);

        builder.HasIndex(i => i.ContainerNumber);
        builder.Ignore(i => i.IsHazardous);
    }
}
