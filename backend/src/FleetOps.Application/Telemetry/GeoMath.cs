namespace FleetOps.Application.Telemetry;

public static class GeoMath
{
    private const double EarthRadiusNauticalMiles = 3440.065;

    /// <summary>Great-circle distance between two fixes, in nautical miles (haversine).</summary>
    public static double DistanceNauticalMiles(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) +
                (Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                 Math.Sin(dLon / 2) * Math.Sin(dLon / 2));

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusNauticalMiles * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
