using FleetOps.Domain.Common;

namespace FleetOps.Domain.Telemetry;

/// <summary>A single IoT sample emitted by a vessel's onboard gateway.</summary>
public sealed class TelemetryReading : Entity
{
    private TelemetryReading() { }

    internal TelemetryReading(
        Guid vesselId,
        DateTime recordedAtUtc,
        double latitude,
        double longitude,
        double speedOverGroundKn,
        int engineRpm,
        double fuelFlowLitresPerHour,
        double engineTempC)
    {
        if (latitude is < -90 or > 90)
        {
            throw new DomainException($"Latitude {latitude} is outside the range -90..90.");
        }

        if (longitude is < -180 or > 180)
        {
            throw new DomainException($"Longitude {longitude} is outside the range -180..180.");
        }

        if (speedOverGroundKn < 0)
        {
            throw new DomainException("Speed over ground cannot be negative.");
        }

        if (engineRpm < 0)
        {
            throw new DomainException("Engine RPM cannot be negative.");
        }

        VesselId = vesselId;
        RecordedAtUtc = recordedAtUtc;
        Latitude = latitude;
        Longitude = longitude;
        SpeedOverGroundKn = speedOverGroundKn;
        EngineRpm = engineRpm;
        FuelFlowLitresPerHour = fuelFlowLitresPerHour;
        EngineTempC = engineTempC;
    }

    public Guid VesselId { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public double SpeedOverGroundKn { get; private set; }

    public int EngineRpm { get; private set; }

    public double FuelFlowLitresPerHour { get; private set; }

    public double EngineTempC { get; private set; }

    /// <summary>
    /// Litres burned per nautical mile. Returns null when stationary, because
    /// fuel-per-distance is undefined at zero speed (a real source of divide-by-zero
    /// bugs in fleet reporting).
    /// </summary>
    public double? FuelPerNauticalMile =>
        SpeedOverGroundKn <= 0.1 ? null : FuelFlowLitresPerHour / SpeedOverGroundKn;
}
