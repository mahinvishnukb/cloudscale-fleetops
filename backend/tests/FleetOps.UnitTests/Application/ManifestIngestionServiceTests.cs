using FleetOps.Application.Common;
using FleetOps.Application.Manifests;
using FleetOps.Domain.Manifests;
using FleetOps.Domain.Vessels;
using FleetOps.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FleetOps.UnitTests.Application;

public sealed class ManifestIngestionServiceTests : IDisposable
{
    private const string Header =
        "container_number,description,gross_weight_kg,origin_port,destination_port,hazard_class";

    private readonly SqliteDbFixture _db = new();
    private readonly FixedClock _clock = new(new DateTime(2026, 7, 30, 9, 0, 0, DateTimeKind.Utc));

    private ManifestIngestionService Service() =>
        new(_db.Context, _clock, NullLogger<ManifestIngestionService>.Instance);

    private async Task<Guid> SeedVesselAsync()
    {
        var vessel = new Vessel(ImoNumber.Create("9074729"), "MV Test", VesselType.ContainerShip, "Vancouver", 50_000);
        _db.Context.Vessels.Add(vessel);
        await _db.Context.SaveChangesAsync();
        return vessel.Id;
    }

    [Fact]
    public async Task Ingesting_a_clean_manifest_accepts_it()
    {
        var vesselId = await SeedVesselAsync();
        var csv = $"{Header}\nCSQU3054383,Machine parts,12000,CAVAN,NLRTM,\nMSKU3820945,Textiles,8000,CAVAN,NLRTM,\n";

        var result = await Service().IngestAsync("V-2026-014", vesselId, "incoming/test.csv", csv);

        Assert.Equal(ManifestStatus.Accepted, result.Status);
        Assert.Equal(2, result.LineItemCount);
        Assert.Equal(20_000m, result.TotalGrossWeightKg);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public async Task Partially_bad_manifest_is_accepted_with_warnings()
    {
        var vesselId = await SeedVesselAsync();
        var csv = $"{Header}\nCSQU3054383,Parts,12000,CAVAN,NLRTM,\nNOTACONTAINER,Junk,900,CAVAN,NLRTM,\n";

        var result = await Service().IngestAsync("V-2026-015", vesselId, "incoming/test.csv", csv);

        Assert.Equal(ManifestStatus.AcceptedWithWarnings, result.Status);
        Assert.Equal(1, result.LineItemCount);
        Assert.Single(result.ValidationErrors);
    }

    [Fact]
    public async Task Manifest_with_no_usable_rows_is_rejected()
    {
        var vesselId = await SeedVesselAsync();
        var csv = $"{Header}\nNOTACONTAINER,Junk,900,CAVAN,NLRTM,\n";

        var result = await Service().IngestAsync("V-2026-016", vesselId, "incoming/test.csv", csv);

        Assert.Equal(ManifestStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task Unknown_vessel_is_rejected_before_any_parsing()
    {
        var csv = $"{Header}\nCSQU3054383,Parts,12000,CAVAN,NLRTM,\n";

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service().IngestAsync("V-2026-017", Guid.NewGuid(), "incoming/test.csv", csv));
    }

    [Fact]
    public async Task Validation_errors_survive_a_round_trip_through_the_database()
    {
        // Guards the ValueConverter + ValueComparer on the errors list: without the
        // comparer, EF cannot see the mutation and the column persists as empty.
        var vesselId = await SeedVesselAsync();
        var csv = $"{Header}\nCSQU3054383,Parts,12000,CAVAN,NLRTM,\nNOTACONTAINER,Junk,900,CAVAN,NLRTM,\n";

        var saved = await Service().IngestAsync("V-2026-018", vesselId, "incoming/test.csv", csv);

        _db.Context.ChangeTracker.Clear();
        var reloaded = await Service().GetAsync(saved.Id);

        Assert.Single(reloaded.Manifest.ValidationErrors);
        Assert.Single(reloaded.LineItems);
        Assert.Equal("CSQU3054383", reloaded.LineItems[0].ContainerNumber);
    }

    [Fact]
    public async Task Processed_timestamp_comes_from_the_injected_clock()
    {
        var vesselId = await SeedVesselAsync();
        var csv = $"{Header}\nCSQU3054383,Parts,12000,CAVAN,NLRTM,\n";

        var result = await Service().IngestAsync("V-2026-019", vesselId, "incoming/test.csv", csv);

        Assert.Equal(_clock.UtcNow, result.ProcessedAtUtc);
    }

    public void Dispose() => _db.Dispose();
}
