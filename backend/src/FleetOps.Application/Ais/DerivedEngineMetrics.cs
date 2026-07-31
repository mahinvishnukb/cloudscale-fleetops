namespace FleetOps.Application.Ais;

/// <summary>
/// DERIVED VALUES — NOT MEASUREMENTS.
///
/// AIS carries navigation data only: position, speed, course, heading. Engine temperature,
/// shaft RPM and fuel flow come from proprietary onboard systems that are not broadcast and
/// are not publicly available anywhere. When the application is running against live AIS,
/// these three figures are modelled from speed over ground rather than observed.
///
/// The model is physically grounded rather than arbitrary: hull resistance rises
/// approximately with the cube of speed, so fuel rate is modelled as a fixed hotel load
/// plus a cubic term. That is why fuel burn per nautical mile falls as a vessel speeds up
/// from rest, bottoms out around economical speed, then climbs again — the shape real
/// operators optimise against.
///
/// Anything surfaced from this class is labelled as derived in the UI and the README.
/// </summary>
public static class DerivedEngineMetrics
{
    /// <summary>Auxiliary and hotel load, burned regardless of speed (litres/hour).</summary>
    private const double HotelLoadLitresPerHour = 40.0;

    /// <summary>Cubic resistance coefficient, calibrated so ~15 kn lands near 90 L/nm.</summary>
    private const double ResistanceCoefficient = 0.39;

    private const double IdleEngineTempC = 30.0;
    private const double TempRiseAtFullPowerC = 48.0;
    private const double ReferenceFullSpeedKn = 20.0;
    private const double RpmPerKnot = 5.5;

    public sealed record Metrics(int EngineRpm, double FuelFlowLitresPerHour, double EngineTempC);

    /// <summary>
    /// Models engine state for a vessel making the given speed. Deterministic for a given
    /// (mmsi, minute) pair so successive readings drift smoothly instead of flickering,
    /// which would otherwise trigger spurious anomalies.
    /// </summary>
    public static Metrics From(double speedOverGroundKn, string mmsi, DateTime atUtc)
    {
        var speed = Math.Max(0, speedOverGroundKn);

        // Stable per-vessel, per-minute jitter in [-1, 1]. No shared Random, so this is
        // thread-safe, and a fixed FNV-1a rather than HashCode.Combine because .NET
        // randomises string hashing per process — the values would otherwise change on
        // every restart and the word "deterministic" would be a lie.
        var jitter = StableJitter(mmsi, atUtc);

        var fuelFlow = HotelLoadLitresPerHour
                       + (ResistanceCoefficient * Math.Pow(speed, 3))
                       + (jitter * 15.0);

        var loadFraction = Math.Clamp(speed / ReferenceFullSpeedKn, 0, 1);
        var engineTemp = IdleEngineTempC
                         + (TempRiseAtFullPowerC * loadFraction)
                         + (jitter * 2.5);

        var rpm = (int)Math.Round((speed * RpmPerKnot) + (jitter * 8));

        return new Metrics(
            Math.Max(0, rpm),
            Math.Round(Math.Max(0, fuelFlow), 1),
            Math.Round(engineTemp, 1));
    }

    /// <summary>
    /// FNV-1a over the MMSI and the current minute, scaled to [-1, 1]. Chosen for stability
    /// across processes rather than for hash quality — this seeds cosmetic variation, not
    /// anything security-sensitive.
    /// </summary>
    private static double StableJitter(string mmsi, DateTime atUtc)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;

        foreach (var c in mmsi)
        {
            hash = (hash ^ c) * prime;
        }

        foreach (var component in new[] { atUtc.Year, atUtc.DayOfYear, atUtc.Hour, atUtc.Minute })
        {
            hash = (hash ^ (uint)component) * prime;
        }

        return ((hash & 0xFFFF) / 65535.0 * 2.0) - 1.0;
    }
}
