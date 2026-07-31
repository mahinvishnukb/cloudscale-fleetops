using FleetOps.Domain.Vessels;
using FleetOps.Infrastructure.Persistence;
using Xunit;

namespace FleetOps.UnitTests.Infrastructure;

/// <summary>
/// Regression guard. The demo fleet originally shipped three IMO numbers with invalid
/// check digits — one of them the very value used as the "invalid" fixture in
/// ImoNumberTests. The domain correctly rejected them, the startup seed threw, the
/// exception was caught and logged, and the application came up with an empty database
/// and no explanation. Seed data is code, and it needs tests like any other code.
/// </summary>
public sealed class DemoFleetTests
{
    public static TheoryData<string, string> Fleet()
    {
        var data = new TheoryData<string, string>();
        foreach (var vessel in DatabaseSeeder.DemoFleet)
        {
            data.Add(vessel.Imo, vessel.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Fleet))]
    public void Every_demo_imo_passes_its_check_digit(string imo, string name)
    {
        Assert.True(
            ImoNumber.TryCreate(imo, out _),
            $"Demo vessel '{name}' has IMO {imo}, which fails check-digit validation. "
            + "Seeding would throw and the database would come up empty.");
    }

    [Theory]
    [MemberData(nameof(Fleet))]
    public void Every_demo_vessel_can_actually_be_constructed(string imo, string name)
    {
        var demo = DatabaseSeeder.DemoFleet.Single(v => v.Imo == imo);

        // Exercises every invariant the seeder will hit: IMO, name, port, tonnage range.
        var vessel = new Vessel(ImoNumber.Create(demo.Imo), demo.Name, demo.Type, demo.Port, demo.Tonnage);

        Assert.Equal(name, vessel.Name);
        Assert.Equal(VesselStatus.InPort, vessel.Status);
    }

    [Fact]
    public void Demo_imos_are_unique()
    {
        // vessels.ImoNumber carries a unique index; duplicates would fail on SaveChanges.
        var imos = DatabaseSeeder.DemoFleet.Select(v => v.Imo).ToList();
        Assert.Equal(imos.Count, imos.Distinct().Count());
    }

    [Fact]
    public void Demo_fleet_is_large_enough_to_be_worth_showing()
        => Assert.True(DatabaseSeeder.DemoFleet.Count >= 5);
}
