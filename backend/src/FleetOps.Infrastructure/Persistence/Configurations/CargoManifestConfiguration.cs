using System.Text.Json;
using FleetOps.Domain.Manifests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FleetOps.Infrastructure.Persistence.Configurations;

public sealed class CargoManifestConfiguration : IEntityTypeConfiguration<CargoManifest>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<CargoManifest> builder)
    {
        builder.ToTable("cargo_manifests");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.VoyageNumber).HasMaxLength(32).IsRequired();
        builder.Property(m => m.SourceObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(m => new { m.VesselId, m.VoyageNumber });

        // Computed in the domain, never persisted.
        builder.Ignore(m => m.TotalGrossWeightKg);
        builder.Ignore(m => m.HazardousCount);
        builder.Ignore(m => m.ValidationErrors);

        // Validation errors are a value list, not entities. Persisted as a JSON string via a
        // converter so the same mapping works on Postgres and on the in-memory provider the
        // tests use. The comparer is not optional: without it EF cannot see mutations to the
        // list and silently skips the UPDATE.
        var errorsConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>());

        var errorsComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
            v => v.ToList());

        builder.Property<List<string>>("_validationErrors")
            .HasColumnName("validation_errors")
            .HasColumnType("text")
            .HasConversion(errorsConverter, errorsComparer)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(m => m.LineItems)
            .WithOne()
            .HasForeignKey(i => i.CargoManifestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(CargoManifest.LineItems))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
