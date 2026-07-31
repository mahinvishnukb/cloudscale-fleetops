using FleetOps.Api.Authorization;
using FleetOps.Application.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetOps.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize(Policy = FleetPolicies.ReadFleet)]
public sealed class TelemetryController(ITelemetryService telemetry) : ControllerBase
{
    /// <summary>Ingests one reading from a vessel gateway and returns any anomalies it triggered.</summary>
    [HttpPost("vessels/{vesselId:guid}/telemetry")]
    [Authorize(Policy = FleetPolicies.ManageFleet)]
    [ProducesResponseType(typeof(RecordTelemetryResult), StatusCodes.Status201Created)]
    public async Task<ActionResult<RecordTelemetryResult>> RecordAsync(
        Guid vesselId, [FromBody] RecordTelemetryRequest request, CancellationToken ct)
    {
        var result = await telemetry.RecordAsync(vesselId, request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Time series for the charts. Defaults to the last 6 hours.</summary>
    [HttpGet("vessels/{vesselId:guid}/telemetry")]
    [ProducesResponseType(typeof(IReadOnlyList<TelemetryReadingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TelemetryReadingDto>>> GetSeriesAsync(
        Guid vesselId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int maxPoints = 500,
        CancellationToken ct = default)
    {
        var toUtc = to ?? DateTime.UtcNow;
        var fromUtc = from ?? toUtc.AddHours(-6);
        return Ok(await telemetry.GetSeriesAsync(vesselId, fromUtc, toUtc, maxPoints, ct));
    }

    [HttpGet("anomalies")]
    [ProducesResponseType(typeof(IReadOnlyList<AnomalyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AnomalyDto>>> GetOpenAnomaliesAsync(
        [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await telemetry.GetOpenAnomaliesAsync(limit, ct));

    [HttpPost("anomalies/{id:guid}/acknowledge")]
    [Authorize(Policy = FleetPolicies.ManageFleet)]
    [ProducesResponseType(typeof(AnomalyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnomalyDto>> AcknowledgeAsync(Guid id, CancellationToken ct)
        => Ok(await telemetry.AcknowledgeAsync(id, ct));

    /// <summary>Aggregate tiles for the dashboard header.</summary>
    [HttpGet("analytics/fleet-health")]
    [ProducesResponseType(typeof(FleetHealthDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FleetHealthDto>> GetFleetHealthAsync(CancellationToken ct)
        => Ok(await telemetry.GetFleetHealthAsync(ct));
}
