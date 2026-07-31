using FleetOps.Domain.Common;
using FleetOps.Domain.Manifests;
using Xunit;

namespace FleetOps.UnitTests.Domain;

public sealed class CargoManifestTests
{
    private static CargoLineItem Item(string container, decimal weight = 12_000) =>
        new(ContainerNumber.Create(container), "Machine parts", weight, "CAVAN", "NLRTM", null);

    private static CargoManifest NewManifest() =>
        new("V-2026-014", Guid.NewGuid(), "incoming/2026/07/30/v-2026-014.csv");

    [Fact]
    public void Rejects_manifest_with_no_readable_rows()
    {
        var manifest = NewManifest();
        manifest.BeginProcessing();
        manifest.AddValidationError("Line 2: unparseable");
        manifest.CompleteProcessing(DateTime.UtcNow);

        Assert.Equal(ManifestStatus.Rejected, manifest.Status);
    }

    [Fact]
    public void Accepts_clean_manifest()
    {
        var manifest = NewManifest();
        manifest.BeginProcessing();
        manifest.AddLineItem(Item("CSQU3054383"));
        manifest.CompleteProcessing(DateTime.UtcNow);

        Assert.Equal(ManifestStatus.Accepted, manifest.Status);
    }

    [Fact]
    public void Good_rows_still_ship_when_some_rows_fail()
    {
        var manifest = NewManifest();
        manifest.BeginProcessing();
        manifest.AddLineItem(Item("CSQU3054383"));
        manifest.AddValidationError("Line 7: bad weight");
        manifest.CompleteProcessing(DateTime.UtcNow);

        Assert.Equal(ManifestStatus.AcceptedWithWarnings, manifest.Status);
        Assert.Single(manifest.LineItems);
    }

    [Fact]
    public void Duplicate_container_in_one_manifest_is_rejected()
    {
        var manifest = NewManifest();
        manifest.BeginProcessing();
        manifest.AddLineItem(Item("CSQU3054383"));

        Assert.Throws<DomainException>(() => manifest.AddLineItem(Item("CSQU3054383")));
    }

    [Fact]
    public void Cannot_begin_processing_twice()
    {
        var manifest = NewManifest();
        manifest.BeginProcessing();

        Assert.Throws<DomainException>(manifest.BeginProcessing);
    }

    [Fact]
    public void Totals_aggregate_across_line_items()
    {
        var manifest = NewManifest();
        manifest.BeginProcessing();
        manifest.AddLineItem(Item("CSQU3054383", 10_000));
        manifest.AddLineItem(Item("MSKU3820945", 8_500));
        manifest.AddLineItem(new CargoLineItem(
            ContainerNumber.Create("TGHU1234567"), "Paint", 4_000, "CAVAN", "NLRTM", "3"));

        Assert.Equal(22_500m, manifest.TotalGrossWeightKg);
        Assert.Equal(1, manifest.HazardousCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    [InlineData(30_481)]
    public void Rejects_implausible_container_weight(decimal weight)
        => Assert.Throws<DomainException>(() => Item("CSQU3054383", weight));
}
