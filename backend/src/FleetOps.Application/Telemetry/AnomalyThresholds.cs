namespace FleetOps.Application.Telemetry;

/// <summary>
/// Tunable limits for the detection rules. Bound from configuration
/// (section "Telemetry:Thresholds") so operations can retune without a redeploy.
/// </summary>
public sealed class AnomalyThresholds
{
    public double EngineTempWarningC { get; set; } = 85;

    public double EngineTempCriticalC { get; set; } = 95;

    /// <summary>No merchant vessel sustains this; above it the sensor is suspect.</summary>
    public double ImplausibleSpeedKn { get; set; } = 45;

    /// <summary>
    /// Below this speed the fuel-per-distance rule is not applied at all. A vessel at
    /// anchor or manoeuvring still burns fuel for hotel load, and dividing that by a
    /// near-zero speed produces meaningless L/nm figures. Roughly steerage way.
    /// </summary>
    public double MinimumSpeedForFuelRuleKn { get; set; } = 3.0;

    /// <summary>Litres per nautical mile above which consumption is flagged.</summary>
    public double FuelPerNauticalMileWarning { get; set; } = 120;

    public double FuelPerNauticalMileCritical { get; set; } = 200;

    /// <summary>Implied speed between two fixes that indicates a GPS jump rather than travel.</summary>
    public double PositionJumpImpliedSpeedKn { get; set; } = 60;

    /// <summary>Silence longer than this counts as a dropout.</summary>
    public TimeSpan SensorDropout { get; set; } = TimeSpan.FromMinutes(15);
}
