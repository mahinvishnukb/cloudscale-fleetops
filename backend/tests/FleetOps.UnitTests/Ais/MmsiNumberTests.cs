using FleetOps.Domain.Common;
using FleetOps.Domain.Vessels;
using Xunit;

namespace FleetOps.UnitTests.Ais;

public sealed class MmsiNumberTests
{
    [Theory]
    [InlineData("316001234")] // Canada
    [InlineData("259000420")] // Norway, from the aisstream.io documentation
    [InlineData("245473000")] // Netherlands
    [InlineData("367719770")] // United States
    public void Accepts_valid_ship_mmsi(string raw)
        => Assert.True(MmsiNumber.TryCreate(raw, out _));

    [Theory]
    [InlineData("00316001")]  // too short
    [InlineData("3160012345")] // too long
    [InlineData("31600123a")] // non-numeric
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_malformed_mmsi(string? raw)
        => Assert.False(MmsiNumber.TryCreate(raw, out _));

    [Theory]
    [InlineData("002241118")] // coast station
    [InlineData("111000001")] // SAR aircraft
    [InlineData("972351360")] // man-overboard beacon
    [InlineData("993682816")] // aid to navigation, from the documentation
    [InlineData("812345678")] // handheld VHF
    public void Rejects_stations_that_are_not_ships(string raw)
    {
        // Live AIS carries plenty of these. Treating a lighthouse as a fleet vessel
        // would be worse than dropping the message.
        Assert.False(MmsiNumber.TryCreate(raw, out _));
    }

    [Fact]
    public void Exposes_the_flag_state_prefix()
        => Assert.Equal(316, MmsiNumber.Create("316001234").Mid);

    [Fact]
    public void Create_throws_on_an_invalid_mmsi()
        => Assert.Throws<DomainException>(() => MmsiNumber.Create("993682816"));
}
