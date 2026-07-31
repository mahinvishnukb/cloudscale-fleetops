using FleetOps.Domain.Telemetry;

namespace FleetOps.Application.Telemetry;

public sealed record RecordTelemetryRequest(
    DateTime? RecordedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedOverGroundKn,
    int EngineRpm,
    double FuelFlowLitresPerHour,
    double EngineTempC);

public sealed record TelemetryReadingDto(
    Guid Id,
    Guid VesselId,
    DateTime RecordedAtUtc,
    double Latitude,
    double Longitude,
    double SpeedOverGroundKn,
    int EngineRpm,
    double FuelFlowLitresPerHour,
    double EngineTempC,
    double? FuelPerNauticalMile);

public sealed record AnomalyDto(
    Guid Id,
    Guid VesselId,
    string VesselName,
    AnomalyKind Kind,
    AnomalySeverity Severity,
    string Detail,
    DateTime DetectedAtUtc,
    bool IsAcknowledged,
    string? AcknowledgedBy);

public sealed record RecordTelemetryResult(TelemetryReadingDto Reading, IReadOnlyList<AnomalyDto> Anomalies);

public sealed record FleetHealthDto(
    int TotalVessels,
    int UnderWay,
    int InPort,
    int InMaintenance,
    int OpenAnomalies,
    int CriticalAnomalies,
    double AverageSpeedKn,
    double AverageEngineTempC);

public static class TelemetryMappings
{
    public static TelemetryReadingDto ToDto(this TelemetryReading r) => new(
        r.Id, r.VesselId, r.RecordedAtUtc, r.Latitude, r.Longitude,
        r.SpeedOverGroundKn, r.EngineRpm, r.FuelFlowLitresPerHour, r.EngineTempC, r.FuelPerNauticalMile);

    public static AnomalyDto ToDto(this Anomaly a, string vesselName) => new(
        a.Id, a.VesselId, vesselName, a.Kind, a.Severity, a.Detail,
        a.DetectedAtUtc, a.IsAcknowledged, a.AcknowledgedBy);
}
