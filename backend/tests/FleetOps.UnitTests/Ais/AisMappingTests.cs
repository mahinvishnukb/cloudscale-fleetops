using FleetOps.Application.Ais;
using FleetOps.Domain.Vessels;
using Xunit;

namespace FleetOps.UnitTests.Ais;

public sealed class AisNavigationalStatusMapTests
{
    [Theory]
    [InlineData(0, VesselStatus.UnderWay)]   // under way using engine
    [InlineData(1, VesselStatus.AtAnchor)]
    [InlineData(3, VesselStatus.UnderWay)]   // restricted manoeuvrability
    [InlineData(4, VesselStatus.UnderWay)]   // constrained by draught
    [InlineData(5, VesselStatus.InPort)]     // moored
    [InlineData(7, VesselStatus.UnderWay)]   // fishing
    [InlineData(8, VesselStatus.UnderWay)]   // sailing
    public void Maps_known_statuses(int code, VesselStatus expected)
        => Assert.Equal(expected, AisNavigationalStatusMap.ToVesselStatus(code));

    [Theory]
    [InlineData(2)]  // not under command
    [InlineData(6)]  // aground
    [InlineData(9)]  // reserved
    [InlineData(14)] // AIS-SART
    [InlineData(15)] // undefined — the transponder default, very common
    [InlineData(99)] // out of range
    public void Refuses_to_invent_a_status_it_cannot_map(int code)
        => Assert.Null(AisNavigationalStatusMap.ToVesselStatus(code));
}

public sealed class AisSentinelTests
{
    [Fact]
    public void Speed_sentinel_is_rejected()
        => Assert.Null(AisSentinels.Speed(102.3));

    [Theory]
    [InlineData(0)]
    [InlineData(12.4)]
    [InlineData(30)]
    public void Real_speeds_pass_through(double sog)
        => Assert.Equal(sog, AisSentinels.Speed(sog));

    [Fact]
    public void Course_sentinel_is_rejected()
        => Assert.Null(AisSentinels.Course(360.0));

    [Fact]
    public void Heading_sentinel_is_rejected()
        => Assert.Null(AisSentinels.Heading(511));

    [Fact]
    public void Position_sentinels_are_rejected()
    {
        // 91/181 is how AIS says "no fix". Passing it to the domain would throw.
        Assert.Null(AisSentinels.Position(91.0, 181.0));
        Assert.Null(AisSentinels.Position(91.0, -63.5));
        Assert.Null(AisSentinels.Position(44.6, 181.0));
    }

    [Fact]
    public void Out_of_range_coordinates_are_rejected()
    {
        Assert.Null(AisSentinels.Position(95.0, 0));
        Assert.Null(AisSentinels.Position(0, -200.0));
    }

    [Fact]
    public void A_real_fix_is_returned()
    {
        var fix = AisSentinels.Position(44.6488, -63.5752); // Halifax
        Assert.NotNull(fix);
        Assert.Equal(44.6488, fix!.Value.Latitude);
    }

    [Fact]
    public void Missing_coordinates_are_rejected()
        => Assert.Null(AisSentinels.Position(null, null));
}

public sealed class AisShipTypeTests
{
    [Theory]
    [InlineData(70, VesselType.ContainerShip)]
    [InlineData(79, VesselType.ContainerShip)]
    [InlineData(80, VesselType.Tanker)]
    [InlineData(52, VesselType.Tug)]
    [InlineData(60, VesselType.RoRo)]
    [InlineData(0, VesselType.Unknown)]
    [InlineData(null, VesselType.Unknown)]
    public void Maps_itu_ship_types(int? code, VesselType expected)
        => Assert.Equal(expected, AisShipType.ToVesselType(code));

    [Fact]
    public void Estimates_tonnage_from_dimensions()
    {
        // A 300 m x 40 m box boat at 14 m draught.
        var dimension = new AisDimension { A = 150, B = 150, C = 20, D = 20 };
        var tonnage = AisShipType.EstimateGrossTonnage(dimension, 14.0);

        Assert.InRange(tonnage, 20_000, 60_000);
    }

    [Fact]
    public void Falls_back_when_dimensions_are_missing()
        => Assert.Equal(1_000, AisShipType.EstimateGrossTonnage(null, null));

    [Fact]
    public void Never_exceeds_the_domain_limit()
    {
        // A nonsense broadcast must not produce a vessel the domain will reject.
        var absurd = new AisDimension { A = 500, B = 500, C = 250, D = 250 };
        Assert.InRange(AisShipType.EstimateGrossTonnage(absurd, 25), 1, 300_000);
    }
}

public sealed class AisTimestampTests
{
    private static readonly DateTime Fallback = new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Parses_the_go_style_timestamp_from_the_feed()
    {
        var parsed = AisTimestamp.ParseOrDefault("2022-12-29 18:22:32.318353 +0000 UTC", Fallback);

        Assert.Equal(2022, parsed.Year);
        Assert.Equal(18, parsed.Hour);
        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a timestamp")]
    public void Falls_back_rather_than_dropping_the_reading(string? raw)
        => Assert.Equal(Fallback, AisTimestamp.ParseOrDefault(raw, Fallback));
}
