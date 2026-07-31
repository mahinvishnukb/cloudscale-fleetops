using FleetOps.Application.Abstractions;
using FleetOps.Application.Common;
using FleetOps.Domain.Manifests;
using FleetOps.Domain.Vessels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetOps.Application.Manifests;

public sealed class ManifestIngestionService(
    IFleetOpsDbContext db,
    IDateTimeProvider clock,
    ILogger<ManifestIngestionService> logger) : IManifestIngestionService
{
    public async Task<CargoManifestDto> IngestAsync(
        string voyageNumber, Guid vesselId, string objectKey, string csvContent, CancellationToken ct = default)
    {
        var vesselExists = await db.Vessels.AnyAsync(v => v.Id == vesselId, ct);
        if (!vesselExists)
        {
            throw new NotFoundException(nameof(Vessel), vesselId);
        }

        var manifest = new CargoManifest(voyageNumber, vesselId, objectKey);
        manifest.BeginProcessing();

        var parsed = ManifestCsvParser.Parse(csvContent);

        foreach (var error in parsed.Errors)
        {
            manifest.AddValidationError(error.ToString());
        }

        foreach (var row in parsed.Rows)
        {
            try
            {
                var item = new CargoLineItem(
                    ContainerNumber.Create(row.ContainerNumber),
                    row.Description,
                    row.GrossWeightKg,
                    row.OriginPort,
                    row.DestinationPort,
                    row.HazardClass);

                manifest.AddLineItem(item);
            }
            catch (Domain.Common.DomainException ex)
            {
                // A row that passes CSV parsing can still break a domain invariant.
                manifest.AddValidationError($"Container {row.ContainerNumber}: {ex.Message}");
            }
        }

        manifest.CompleteProcessing(clock.UtcNow);

        db.CargoManifests.Add(manifest);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Manifest {Voyage} ingested from {ObjectKey}: {Accepted} accepted, {Errors} error(s), status {Status}",
            manifest.VoyageNumber, objectKey, manifest.LineItems.Count, manifest.ValidationErrors.Count, manifest.Status);

        return manifest.ToDto();
    }

    public async Task<IReadOnlyList<CargoManifestDto>> ListAsync(
        Guid? vesselId, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        var query = db.CargoManifests.AsNoTracking().Include(m => m.LineItems).AsQueryable();

        if (vesselId is not null)
        {
            query = query.Where(m => m.VesselId == vesselId);
        }

        var manifests = await query
            .OrderByDescending(m => m.ReceivedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        return manifests.Select(m => m.ToDto()).ToList();
    }

    public async Task<CargoManifestDetailDto> GetAsync(Guid manifestId, CancellationToken ct = default)
    {
        var manifest = await db.CargoManifests
            .AsNoTracking()
            .Include(m => m.LineItems)
            .FirstOrDefaultAsync(m => m.Id == manifestId, ct)
            ?? throw new NotFoundException(nameof(CargoManifest), manifestId);

        return new CargoManifestDetailDto(
            manifest.ToDto(),
            manifest.LineItems.Select(i => i.ToDto()).ToList());
    }
}
