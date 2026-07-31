using FleetOps.Application.Abstractions;
using FleetOps.Application.Common;
using FleetOps.Domain.Common;
using FleetOps.Domain.Vessels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetOps.Application.Vessels;

public sealed class VesselService(
    IFleetOpsDbContext db,
    ILogger<VesselService> logger) : IVesselService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<VesselSummaryDto>> SearchAsync(
        string? search, VesselStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Vessels.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(v =>
                EF.Functions.Like(v.Name, $"%{term}%") ||
                EF.Functions.Like(v.ImoNumber, $"%{term}%") ||
                EF.Functions.Like(v.HomePort, $"%{term}%"));
        }

        if (status is not null)
        {
            query = query.Where(v => v.Status == status);
        }

        var total = await query.CountAsync(ct);

        var vessels = await query
            .OrderBy(v => v.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = vessels.Select(v => v.Id).ToList();

        // One round trip for the latest reading per vessel on this page.
        var latest = await db.TelemetryReadings
            .AsNoTracking()
            .Where(t => ids.Contains(t.VesselId))
            .GroupBy(t => t.VesselId)
            .Select(g => g.OrderByDescending(t => t.RecordedAtUtc).First())
            .ToListAsync(ct);

        var anomalyCounts = await db.Anomalies
            .AsNoTracking()
            .Where(a => ids.Contains(a.VesselId) && !a.IsAcknowledged)
            .GroupBy(a => a.VesselId)
            .Select(g => new { VesselId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var latestByVessel = latest.ToDictionary(t => t.VesselId);
        var countsByVessel = anomalyCounts.ToDictionary(a => a.VesselId, a => a.Count);

        var items = vessels.Select(v =>
        {
            latestByVessel.TryGetValue(v.Id, out var reading);
            countsByVessel.TryGetValue(v.Id, out var openAnomalies);

            return new VesselSummaryDto(
                v.Id, v.ImoNumber, v.Name, v.Type, v.Status, v.HomePort, v.GrossTonnage,
                reading?.SpeedOverGroundKn,
                reading?.EngineTempC,
                reading?.RecordedAtUtc,
                openAnomalies);
        }).ToList();

        return new PagedResult<VesselSummaryDto>(items, page, pageSize, total);
    }

    public async Task<VesselDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var vessel = await db.Vessels.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException(nameof(Vessel), id);

        return vessel.ToDto();
    }

    public async Task<VesselDto> CreateAsync(CreateVesselRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var imo = ImoNumber.Create(request.ImoNumber);

        var duplicate = await db.Vessels.AnyAsync(v => v.ImoNumber == imo.Value, ct);
        if (duplicate)
        {
            throw new DomainException($"A vessel with IMO {imo.Value} already exists.");
        }

        var vessel = new Vessel(imo, request.Name, request.Type, request.HomePort, request.GrossTonnage);

        db.Vessels.Add(vessel);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Vessel {Imo} ({Name}) registered with id {VesselId}",
            vessel.ImoNumber, vessel.Name, vessel.Id);

        return vessel.ToDto();
    }

    public async Task<VesselDto> ChangeStatusAsync(Guid id, VesselStatus status, CancellationToken ct = default)
    {
        var vessel = await db.Vessels.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException(nameof(Vessel), id);

        vessel.ChangeStatus(status);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Vessel {VesselId} status changed to {Status}", id, status);
        return vessel.ToDto();
    }

    public async Task DecommissionAsync(Guid id, CancellationToken ct = default)
    {
        var vessel = await db.Vessels.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException(nameof(Vessel), id);

        vessel.Decommission();
        await db.SaveChangesAsync(ct);

        logger.LogWarning("Vessel {VesselId} decommissioned", id);
    }
}
