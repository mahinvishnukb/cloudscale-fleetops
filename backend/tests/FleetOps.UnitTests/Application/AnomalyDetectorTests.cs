using FleetOps.Application.Telemetry;
using FleetOps.Domain.Telemetry;
using FleetOps.UnitTests.Support;
using Xunit;

namespace FleetOps.UnitTests.Application;

public sealed class AnomalyDetectorTests
{
    private static readonly DateTime Now = new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

    private static AnomalyDetector Detector() => new(new AnomalyThresholds());

    [Fact]
    public void Nominal_reading_raises_nothing()
    {
        var vessel = TelemetryFactory.Vessel();
        var reading = TelemetryFactory.Reading(vessel, Now, speedKn: 12, fuelFlow: 600, engineTempC: 70);

        Assert.Empty(Detector().Evaluate(reading, null, Now));
    }

    [Fact]
    public void Engine_temperature_above_critical_raises_critical()
    {
        var vessel = TelemetryFactory.Vessel();
        var reading = TelemetryFactory.Reading(vessel, Now, engineTempC: 99);

        var anomaly = Assert.Single(
            Detector().Evaluate(reading, null, Now).Where(a => a.Kind == AnomalyKind.EngineOverheat));

        Assert.Equal(AnomalySeverity.Critical, anomaly.Severity);
    }

    [Fact]
    public void Engine_temperature_between_thresholds_raises_warning()
    {
        var vessel = TelemetryFactory.Vessel();
        var reading = TelemetryFactory.Reading(vessel, Now, engineTempC: 88);

        var anomaly = Assert.Single(
            Detector().Evaluate(reading, null, Now).Where(a => a.Kind == AnomalyKind.EngineOverheat));

        Assert.Equal(AnomalySeverity.Warning, anomaly.Severity);
    }

    [Fact]
    public void Boundary_temperature_is_inclusive()
    {
        var vessel = TelemetryFactory.Vessel();
        var atWarning = TelemetryFactory.Reading(vessel, Now, engineTempC: 85);
        var justBelow = TelemetryFactory.Reading(vessel, Now, engineTempC: 84.9);

        Assert.Contains(Detector().Evaluate(atWarning, null, Now), a => a.Kind == AnomalyKind.EngineOverheat);
        Assert.DoesNotContain(Detector().Evaluate(justBelow, null, Now), a => a.Kind == AnomalyKind.EngineOverheat);
    }

    [Fact]
    public void Implausible_speed_is_flagged()
    {
        var vessel = TelemetryFactory.Vessel();
        var reading = TelemetryFactory.Reading(vessel, Now, speedKn: 60, fuelFlow: 600);

        Assert.Contains(Detector().Evaluate(reading, null, Now), a => a.Kind == AnomalyKind.ImplausibleSpeed);
    }

    [Fact]
    public void Stationary_vessel_never_raises_a_fuel_anomaly()
    {
        // Fuel-per-mile is undefined at zero speed; the rule must skip, not divide.
        var vessel = TelemetryFactory.Vessel();
        var reading = TelemetryFactory.Reading(vessel, Now, speedKn: 0, fuelFlow: 5_000);

        Assert.DoesNotContain(
            Detector().Evaluate(reading, null, Now),
            a => a.Kind == AnomalyKind.FuelConsumptionSpike);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.3)]
    [InlineData(0.4)]
    [InlineData(2.9)]
    public void Vessel_below_steerage_way_never_raises_a_fuel_anomaly(double speedKn)
    {
        // Regression: guarding only against exactly-zero speed was not enough. A vessel
        // at anchor drifting at 0.3 kn while burning a normal 45 L/h hotel load divides
        // out to 150 L/nm and flooded the dashboard with false warnings.
        var vessel = TelemetryFactory.Vessel();
        var reading = TelemetryFactory.Reading(vessel, Now, speedKn: speedKn, fuelFlow: 45);

        Assert.DoesNotContain(
            Detector().Evaluate(reading, null, Now),
            a => a.Kind == AnomalyKind.FuelConsumptionSpike);
    }

    [Fact]
    public void Fuel_rule_applies_again_once_the_vessel_is_making_way()
    {
        // 3 kn against 400 L/h = 133 L/nm, over the 120 warning threshold.
        var vessel = TelemetryFactory.Vessel();
        var reading = TelemetryFactory.Reading(vessel, Now, speedKn: 3.0, fuelFlow: 400);

        Assert.Contains(
            Detector().Evaluate(reading, null, Now),
            a => a.Kind == AnomalyKind.FuelConsumptionSpike);
    }

    [Fact]
    public void Minimum_speed_for_the_fuel_rule_is_configurable()
    {
        var lenient = new AnomalyDetector(new AnomalyThresholds { MinimumSpeedForFuelRuleKn = 0.05 });
        var vessel = TelemetryFactory.Vessel();
        var reading = TelemetryFactory.Reading(vessel, Now, speedKn: 0.3, fuelFlow: 45);

        Assert.Contains(
            lenient.Evaluate(reading, null, Now),
            a => a.Kind == AnomalyKind.FuelConsumptionSpike);
    }

    [Fact]
    public void Excessive_fuel_burn_is_flagged_as_critical()
    {
        // 10 kn against 2,500 L/h = 250 L/nm, past the 200 critical threshold.
        var vessel = TelemetryFactory.Vessel();
        var reading = TelemetryFactory.Reading(vessel, Now, speedKn: 10, fuelFlow: 2_500);

        var anomaly = Assert.Single(
            Detector().Evaluate(reading, null, Now).Where(a => a.Kind == AnomalyKind.FuelConsumptionSpike));

        Assert.Equal(AnomalySeverity.Critical, anomaly.Severity);
    }

    [Fact]
    public void Teleporting_position_is_flagged_as_a_gps_jump()
    {
        var vessel = TelemetryFactory.Vessel();
        var previous = TelemetryFactory.Reading(vessel, Now.AddMinutes(-5), lat: 49.28, lon: -123.12);
        var current = TelemetryFactory.Reading(vessel, Now, lat: 51.50, lon: -0.12); // Vancouver -> London

        Assert.Contains(
            Detector().Evaluate(current, previous, Now),
            a => a.Kind == AnomalyKind.PositionJump && a.Severity == AnomalySeverity.Critical);
    }

    [Fact]
    public void Normal_movement_is_not_a_position_jump()
    {
        var vessel = TelemetryFactory.Vessel();
        var previous = TelemetryFactory.Reading(vessel, Now.AddMinutes(-10), lat: 49.28, lon: -123.12);
        var current = TelemetryFactory.Reading(vessel, Now, lat: 49.31, lon: -123.12);

        Assert.DoesNotContain(
            Detector().Evaluate(current, previous, Now),
            a => a.Kind == AnomalyKind.PositionJump);
    }

    [Fact]
    public void Long_silence_is_reported_as_a_sensor_dropout()
    {
        var vessel = TelemetryFactory.Vessel();
        var previous = TelemetryFactory.Reading(vessel, Now.AddMinutes(-40), lat: 49.28, lon: -123.12);
        var current = TelemetryFactory.Reading(vessel, Now, lat: 49.29, lon: -123.13);

        Assert.Contains(
            Detector().Evaluate(current, previous, Now),
            a => a.Kind == AnomalyKind.SensorDropout);
    }

    [Fact]
    public void Thresholds_are_configurable()
    {
        var strict = new AnomalyDetector(new AnomalyThresholds { EngineTempWarningC = 60, EngineTempCriticalC = 65 });
        var vessel = TelemetryFactory.Vessel();
        var reading = TelemetryFactory.Reading(vessel, Now, engineTempC: 70);

        Assert.Contains(
            strict.Evaluate(reading, null, Now),
            a => a.Kind == AnomalyKind.EngineOverheat && a.Severity == AnomalySeverity.Critical);
    }

    [Fact]
    public void Detector_carries_vessel_and_reading_ids_onto_the_anomaly()
    {
        var vessel = TelemetryFactory.Vessel();
        var reading = TelemetryFactory.Reading(vessel, Now, engineTempC: 99);

        var anomaly = Detector().Evaluate(reading, null, Now).First();

        Assert.Equal(vessel.Id, anomaly.VesselId);
        Assert.Equal(reading.Id, anomaly.TelemetryReadingId);
        Assert.Equal(Now, anomaly.DetectedAtUtc);
    }
}
