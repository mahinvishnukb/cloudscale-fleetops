using FleetOps.Application.Telemetry;
using Xunit;

namespace FleetOps.UnitTests.Application;

public sealed class GeoMathTests
{
    [Fact]
    public void Distance_to_self_is_zero()
        => Assert.Equal(0d, GeoMath.DistanceNauticalMiles(49.28, -123.12, 49.28, -123.12), 6);

    [Fact]
    public void One_minute_of_latitude_is_one_nautical_mile()
    {
        // The definition of the nautical mile. Tolerance covers the spherical approximation.
        var distance = GeoMath.DistanceNauticalMiles(0, 0, 1.0 / 60.0, 0);
        Assert.InRange(distance, 0.99, 1.01);
    }

    [Fact]
    public void Vancouver_to_london_is_roughly_four_thousand_two_hundred_nautical_miles()
    {
        var distance = GeoMath.DistanceNauticalMiles(49.28, -123.12, 51.50, -0.12);
        Assert.InRange(distance, 4_000, 4_400);
    }

    [Fact]
    public void Distance_is_symmetric()
    {
        var forward = GeoMath.DistanceNauticalMiles(10, 20, 30, 40);
        var backward = GeoMath.DistanceNauticalMiles(30, 40, 10, 20);
        Assert.Equal(forward, backward, 9);
    }
}
