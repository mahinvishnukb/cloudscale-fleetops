using FleetOps.Domain.Common;

namespace FleetOps.Domain.Telemetry;

/// <summary>An anomaly raised by the detection rules against one telemetry reading.</summary>
public sealed class Anomaly : Entity
{
    private Anomaly()
    {
        Detail = string.Empty;
    }

    public Anomaly(
        Guid vesselId,
        Guid? telemetryReadingId,
        AnomalyKind kind,
        AnomalySeverity severity,
        string detail,
        DateTime detectedAtUtc)
    {
        VesselId = vesselId;
        TelemetryReadingId = telemetryReadingId;
        Kind = kind;
        Severity = severity;
        Detail = detail;
        DetectedAtUtc = detectedAtUtc;
    }

    public Guid VesselId { get; private set; }

    public Guid? TelemetryReadingId { get; private set; }

    public AnomalyKind Kind { get; private set; }

    public AnomalySeverity Severity { get; private set; }

    public string Detail { get; private set; }

    public DateTime DetectedAtUtc { get; private set; }

    public bool IsAcknowledged { get; private set; }

    public string? AcknowledgedBy { get; private set; }

    public void Acknowledge(string username)
    {
        if (IsAcknowledged)
        {
            throw new DomainException("Anomaly has already been acknowledged.");
        }

        IsAcknowledged = true;
        AcknowledgedBy = username;
        Touch();
    }
}
