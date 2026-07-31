using FleetOps.Domain.Common;
using FleetOps.Domain.Vessels;
using Xunit;

namespace FleetOps.UnitTests.Domain;

public sealed class VesselTests
{
    private static Vessel NewVessel() =>
        new(ImoNumber.Create("9074729"), "MV Test", VesselType.ContainerShip, "Vancouver", 50_000);

    [Fact]
    public void New_vessel_starts_in_port()
        => Assert.Equal(VesselStatus.InPort, NewVessel().Status);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(300_001)]
    public void Rejects_implausible_gross_tonnage(int tonnage)
        => Assert.Throws<DomainException>(() =>
            new Vessel(ImoNumber.Create("9074729"), "MV Test", VesselType.Tanker, "Halifax", tonnage));

    [Fact]
    public void Decommissioned_vessel_cannot_change_status()
    {
        var vessel = NewVessel();
        vessel.Decommission();

        var ex = Assert.Throws<DomainException>(() => vessel.ChangeStatus(VesselStatus.UnderWay));
        Assert.Contains("decommissioned", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Decommissioned_vessel_cannot_report_telemetry()
    {
        var vessel = NewVessel();
        vessel.Decommission();

        Assert.Throws<DomainException>(() =>
            vessel.RecordTelemetry(DateTime.UtcNow, 49.2, -123.1, 12, 80, 500, 70));
    }

    [Fact]
    public void Cannot_set_status_to_unknown()
        => Assert.Throws<DomainException>(() => NewVessel().ChangeStatus(VesselStatus.Unknown));

    [Theory]
    [InlineData(91.0, 0)]
    [InlineData(-91.0, 0)]
    [InlineData(0, 181.0)]
    [InlineData(0, -181.0)]
    public void Rejects_out_of_range_coordinates(double latitude, double longitude)
        => Assert.Throws<DomainException>(() =>
            NewVessel().RecordTelemetry(DateTime.UtcNow, latitude, longitude, 10, 80, 400, 70));

    [Fact]
    public void Fuel_per_nautical_mile_is_null_when_stationary()
    {
        var reading = NewVessel().RecordTelemetry(DateTime.UtcNow, 49.2, -123.1, 0, 0, 40, 32);
        Assert.Null(reading.FuelPerNauticalMile);
    }

    [Fact]
    public void Fuel_per_nautical_mile_divides_flow_by_speed()
    {
        var reading = NewVessel().RecordTelemetry(DateTime.UtcNow, 49.2, -123.1, 10, 80, 900, 70);
        Assert.Equal(90d, reading.FuelPerNauticalMile!.Value, 6);
    }
}
