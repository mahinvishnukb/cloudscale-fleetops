namespace FleetOps.Application.Telemetry;

public interface ITelemetryService
{
    Task<RecordTelemetryResult> RecordAsync(Guid vesselId, RecordTelemetryRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<TelemetryReadingDto>> GetSeriesAsync(
        Guid vesselId, DateTime fromUtc, DateTime toUtc, int maxPoints, CancellationToken ct = default);

    Task<IReadOnlyList<AnomalyDto>> GetOpenAnomaliesAsync(int limit, CancellationToken ct = default);

    Task<AnomalyDto> AcknowledgeAsync(Guid anomalyId, CancellationToken ct = default);

    Task<FleetHealthDto> GetFleetHealthAsync(CancellationToken ct = default);
}
