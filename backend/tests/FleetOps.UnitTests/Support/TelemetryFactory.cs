using FleetOps.Domain.Telemetry;
using FleetOps.Domain.Vessels;

namespace FleetOps.UnitTests.Support;

/// <summary>
/// TelemetryReading's constructor is internal to the domain by design — readings can only
/// be created through a Vessel. Tests go through the same door.
/// </summary>
internal static class TelemetryFactory
{
    public static Vessel Vessel(string imo = "9074729") =>
        new(ImoNumber.Create(imo), "MV Test", VesselType.ContainerShip, "Vancouver", 50_000);

    public static TelemetryReading Reading(
        Vessel vessel,
        DateTime? at = null,
        double lat = 49.28,
        double lon = -123.12,
        double speedKn = 12,
        int rpm = 80,
        double fuelFlow = 600,
        double engineTempC = 70)
        => vessel.RecordTelemetry(at ?? DateTime.UtcNow, lat, lon, speedKn, rpm, fuelFlow, engineTempC);
}
