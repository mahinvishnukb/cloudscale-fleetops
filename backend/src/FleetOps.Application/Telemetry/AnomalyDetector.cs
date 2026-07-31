using FleetOps.Domain.Telemetry;

namespace FleetOps.Application.Telemetry;

/// <summary>
/// Pure rules engine: given a reading and its predecessor, decide what is wrong.
/// Deliberately free of I/O, clocks and DI so every rule is directly unit-testable.
/// </summary>
public sealed class AnomalyDetector(AnomalyThresholds thresholds)
{
    private readonly AnomalyThresholds _thresholds = thresholds
        ?? throw new ArgumentNullException(nameof(thresholds));

    public IReadOnlyList<Anomaly> Evaluate(
        TelemetryReading current,
        TelemetryReading? previous,
        DateTime detectedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(current);

        var anomalies = new List<Anomaly>();

        EvaluateEngineTemperature(current, detectedAtUtc, anomalies);
        EvaluateSpeed(current, detectedAtUtc, anomalies);
        EvaluateFuelBurn(current, detectedAtUtc, anomalies);

        if (previous is not null)
        {
            EvaluatePositionJump(current, previous, detectedAtUtc, anomalies);
            EvaluateDropout(current, previous, detectedAtUtc, anomalies);
        }

        return anomalies;
    }

    private void EvaluateEngineTemperature(TelemetryReading r, DateTime at, List<Anomaly> sink)
    {
        if (r.EngineTempC >= _thresholds.EngineTempCriticalC)
        {
            sink.Add(Raise(r, AnomalyKind.EngineOverheat, AnomalySeverity.Critical, at,
                $"Engine temperature {r.EngineTempC:F1} °C is at or above the critical limit of {_thresholds.EngineTempCriticalC:F1} °C."));
        }
        else if (r.EngineTempC >= _thresholds.EngineTempWarningC)
        {
            sink.Add(Raise(r, AnomalyKind.EngineOverheat, AnomalySeverity.Warning, at,
                $"Engine temperature {r.EngineTempC:F1} °C exceeds the warning limit of {_thresholds.EngineTempWarningC:F1} °C."));
        }
    }

    private void EvaluateSpeed(TelemetryReading r, DateTime at, List<Anomaly> sink)
    {
        if (r.SpeedOverGroundKn > _thresholds.ImplausibleSpeedKn)
        {
            sink.Add(Raise(r, AnomalyKind.ImplausibleSpeed, AnomalySeverity.Warning, at,
                $"Reported speed {r.SpeedOverGroundKn:F1} kn exceeds the plausible maximum of {_thresholds.ImplausibleSpeedKn:F1} kn; suspect a GPS or log-sensor fault."));
        }
    }

    private void EvaluateFuelBurn(TelemetryReading r, DateTime at, List<Anomaly> sink)
    {
        // Undefined while stationary — skip rather than divide by ~zero.
        if (r.FuelPerNauticalMile is not { } burn)
        {
            return;
        }

        // Guarding against zero is not enough. A vessel at anchor still burns fuel for
        // hotel load and auxiliaries, so at 0.3 kn a perfectly normal 45 L/h divides out
        // to 150 L/nm and trips the alarm. Fuel-per-distance only means anything once the
        // vessel is actually making way, so the rule does not apply below steerage speed.
        if (r.SpeedOverGroundKn < _thresholds.MinimumSpeedForFuelRuleKn)
        {
            return;
        }

        if (burn >= _thresholds.FuelPerNauticalMileCritical)
        {
            sink.Add(Raise(r, AnomalyKind.FuelConsumptionSpike, AnomalySeverity.Critical, at,
                $"Fuel burn {burn:F1} L/nm is at or above the critical limit of {_thresholds.FuelPerNauticalMileCritical:F1} L/nm."));
        }
        else if (burn >= _thresholds.FuelPerNauticalMileWarning)
        {
            sink.Add(Raise(r, AnomalyKind.FuelConsumptionSpike, AnomalySeverity.Warning, at,
                $"Fuel burn {burn:F1} L/nm exceeds the warning limit of {_thresholds.FuelPerNauticalMileWarning:F1} L/nm."));
        }
    }

    private void EvaluatePositionJump(TelemetryReading current, TelemetryReading previous, DateTime at, List<Anomaly> sink)
    {
        var elapsedHours = (current.RecordedAtUtc - previous.RecordedAtUtc).TotalHours;
        if (elapsedHours <= 0)
        {
            return;
        }

        var distance = GeoMath.DistanceNauticalMiles(
            previous.Latitude, previous.Longitude, current.Latitude, current.Longitude);

        var impliedSpeed = distance / elapsedHours;

        if (impliedSpeed > _thresholds.PositionJumpImpliedSpeedKn)
        {
            sink.Add(Raise(current, AnomalyKind.PositionJump, AnomalySeverity.Critical, at,
                $"Position moved {distance:F1} nm in {elapsedHours * 60:F0} min, implying {impliedSpeed:F0} kn. Treating as a GPS jump."));
        }
    }

    private void EvaluateDropout(TelemetryReading current, TelemetryReading previous, DateTime at, List<Anomaly> sink)
    {
        var gap = current.RecordedAtUtc - previous.RecordedAtUtc;
        if (gap > _thresholds.SensorDropout)
        {
            sink.Add(Raise(current, AnomalyKind.SensorDropout, AnomalySeverity.Warning, at,
                $"No telemetry for {gap.TotalMinutes:F0} min, exceeding the {_thresholds.SensorDropout.TotalMinutes:F0} min dropout threshold."));
        }
    }

    private static Anomaly Raise(
        TelemetryReading reading,
        AnomalyKind kind,
        AnomalySeverity severity,
        DateTime detectedAtUtc,
        string detail) =>
        new(reading.VesselId, reading.Id, kind, severity, detail, detectedAtUtc);
}
