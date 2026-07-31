using FleetOps.Application.Abstractions;
using FleetOps.Application.Common;
using FleetOps.Domain.Telemetry;
using FleetOps.Domain.Vessels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetOps.Application.Telemetry;

public sealed class TelemetryService(
    IFleetOpsDbContext db,
    AnomalyDetector detector,
    ITelemetryBroadcaster broadcaster,
    IDateTimeProvider clock,
    ICurrentUser currentUser,
    ILogger<TelemetryService> logger) : ITelemetryService
{
    public async Task<RecordTelemetryResult> RecordAsync(
        Guid vesselId, RecordTelemetryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var vessel = await db.Vessels.FirstOrDefaultAsync(v => v.Id == vesselId, ct)
            ?? throw new NotFoundException(nameof(Vessel), vesselId);

        var previous = await db.TelemetryReadings
            .AsNoTracking()
            .Where(t => t.VesselId == vesselId)
            .OrderByDescending(t => t.RecordedAtUtc)
            .FirstOrDefaultAsync(ct);

        var reading = vessel.RecordTelemetry(
            request.RecordedAtUtc ?? clock.UtcNow,
            request.Latitude,
            request.Longitude,
            request.SpeedOverGroundKn,
            request.EngineRpm,
            request.FuelFlowLitresPerHour,
            request.EngineTempC);

        db.TelemetryReadings.Add(reading);

        var anomalies = detector.Evaluate(reading, previous, clock.UtcNow);
        foreach (var anomaly in anomalies)
        {
            db.Anomalies.Add(anomaly);
        }

        await db.SaveChangesAsync(ct);

        await broadcaster.BroadcastReadingAsync(reading, ct);
        foreach (var anomaly in anomalies)
        {
            await broadcaster.BroadcastAnomalyAsync(anomaly, ct);

            logger.LogWarning(
                "Anomaly {Kind} ({Severity}) raised for vessel {VesselName}: {Detail}",
                anomaly.Kind, anomaly.Severity, vessel.Name, anomaly.Detail);
        }

        return new RecordTelemetryResult(
            reading.ToDto(),
            anomalies.Select(a => a.ToDto(vessel.Name)).ToList());
    }

    public async Task<IReadOnlyList<TelemetryReadingDto>> GetSeriesAsync(
        Guid vesselId, DateTime fromUtc, DateTime toUtc, int maxPoints, CancellationToken ct = default)
    {
        maxPoints = Math.Clamp(maxPoints, 10, 5_000);

        var exists = await db.Vessels.AnyAsync(v => v.Id == vesselId, ct);
        if (!exists)
        {
            throw new NotFoundException(nameof(Vessel), vesselId);
        }

        var readings = await db.TelemetryReadings
            .AsNoTracking()
            .Where(t => t.VesselId == vesselId && t.RecordedAtUtc >= fromUtc && t.RecordedAtUtc <= toUtc)
            .OrderByDescending(t => t.RecordedAtUtc)
            .Take(maxPoints)
            .ToListAsync(ct);

        return readings
            .OrderBy(t => t.RecordedAtUtc)
            .Select(t => t.ToDto())
            .ToList();
    }

    public async Task<IReadOnlyList<AnomalyDto>> GetOpenAnomaliesAsync(int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        var rows = await db.Anomalies
            .AsNoTracking()
            .Where(a => !a.IsAcknowledged)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.DetectedAtUtc)
            .Take(limit)
            .Join(db.Vessels.AsNoTracking(),
                  a => a.VesselId,
                  v => v.Id,
                  (a, v) => new { Anomaly = a, VesselName = v.Name })
            .ToListAsync(ct);

        return rows.Select(r => r.Anomaly.ToDto(r.VesselName)).ToList();
    }

    public async Task<AnomalyDto> AcknowledgeAsync(Guid anomalyId, CancellationToken ct = default)
    {
        var anomaly = await db.Anomalies.FirstOrDefaultAsync(a => a.Id == anomalyId, ct)
            ?? throw new NotFoundException(nameof(Anomaly), anomalyId);

        anomaly.Acknowledge(currentUser.Username ?? "system");
        await db.SaveChangesAsync(ct);

        var vesselName = await db.Vessels
            .AsNoTracking()
            .Where(v => v.Id == anomaly.VesselId)
            .Select(v => v.Name)
            .FirstOrDefaultAsync(ct) ?? "Unknown";

        return anomaly.ToDto(vesselName);
    }

    public async Task<FleetHealthDto> GetFleetHealthAsync(CancellationToken ct = default)
    {
        var statusCounts = await db.Vessels
            .AsNoTracking()
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var total = statusCounts.Sum(s => s.Count);
        int CountFor(VesselStatus status) =>
            statusCounts.FirstOrDefault(s => s.Status == status)?.Count ?? 0;

        var openAnomalies = await db.Anomalies.CountAsync(a => !a.IsAcknowledged, ct);
        var critical = await db.Anomalies
            .CountAsync(a => !a.IsAcknowledged && a.Severity == AnomalySeverity.Critical, ct);

        // Averages over the last hour only; a fleet-wide all-time average is meaningless.
        var since = clock.UtcNow.AddHours(-1);
        var recent = await db.TelemetryReadings
            .AsNoTracking()
            .Where(t => t.RecordedAtUtc >= since)
            .Select(t => new { t.SpeedOverGroundKn, t.EngineTempC })
            .ToListAsync(ct);

        return new FleetHealthDto(
            total,
            CountFor(VesselStatus.UnderWay),
            CountFor(VesselStatus.InPort),
            CountFor(VesselStatus.Maintenance),
            openAnomalies,
            critical,
            recent.Count == 0 ? 0 : Math.Round(recent.Average(r => r.SpeedOverGroundKn), 2),
            recent.Count == 0 ? 0 : Math.Round(recent.Average(r => r.EngineTempC), 2));
    }
}
