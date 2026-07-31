namespace FleetOps.Application.Ais;

/// <summary>
/// AIS encodes "not available" as in-band sentinel values rather than nulls. Treating them
/// as real readings is the classic way to get a fleet of ships doing 102.3 knots at
/// latitude 91, so every one is filtered before the data reaches the domain.
/// </summary>
public static class AisSentinels
{
    /// <summary>Speed over ground: 102.3 kn means unavailable.</summary>
    public const double SpeedUnavailable = 102.3;

    /// <summary>Course over ground: 360 degrees means unavailable.</summary>
    public const double CourseUnavailable = 360.0;

    /// <summary>True heading: 511 means unavailable.</summary>
    public const int HeadingUnavailable = 511;

    /// <summary>Latitude: 91 means unavailable.</summary>
    public const double LatitudeUnavailable = 91.0;

    /// <summary>Longitude: 181 means unavailable.</summary>
    public const double LongitudeUnavailable = 181.0;

    public static double? Speed(double? sog) =>
        sog is null || sog >= SpeedUnavailable || sog < 0 ? null : sog;

    public static double? Course(double? cog) =>
        cog is null || cog >= CourseUnavailable || cog < 0 ? null : cog;

    public static int? Heading(int? heading) =>
        heading is null || heading >= HeadingUnavailable || heading < 0 ? null : heading;

    /// <summary>Returns the fix only when both coordinates are present and in range.</summary>
    public static (double Latitude, double Longitude)? Position(double? latitude, double? longitude)
    {
        if (latitude is null || longitude is null)
        {
            return null;
        }

        if (Math.Abs(latitude.Value) >= LatitudeUnavailable || Math.Abs(longitude.Value) >= LongitudeUnavailable)
        {
            return null;
        }

        if (latitude.Value is < -90 or > 90 || longitude.Value is < -180 or > 180)
        {
            return null;
        }

        return (latitude.Value, longitude.Value);
    }
}
