using FleetOps.Domain.Manifests;

namespace FleetOps.Application.Manifests;

public sealed record CargoLineItemDto(
    Guid Id,
    string ContainerNumber,
    string Description,
    decimal GrossWeightKg,
    string OriginPort,
    string DestinationPort,
    string? HazardClass);

public sealed record CargoManifestDto(
    Guid Id,
    string VoyageNumber,
    Guid VesselId,
    string SourceObjectKey,
    ManifestStatus Status,
    DateTime ReceivedAtUtc,
    DateTime? ProcessedAtUtc,
    int LineItemCount,
    decimal TotalGrossWeightKg,
    int HazardousCount,
    IReadOnlyList<string> ValidationErrors);

public sealed record CargoManifestDetailDto(
    CargoManifestDto Manifest,
    IReadOnlyList<CargoLineItemDto> LineItems);

public static class ManifestMappings
{
    public static CargoLineItemDto ToDto(this CargoLineItem item) => new(
        item.Id, item.ContainerNumber, item.Description, item.GrossWeightKg,
        item.OriginPort, item.DestinationPort, item.HazardClass);

    public static CargoManifestDto ToDto(this CargoManifest manifest) => new(
        manifest.Id,
        manifest.VoyageNumber,
        manifest.VesselId,
        manifest.SourceObjectKey,
        manifest.Status,
        manifest.ReceivedAtUtc,
        manifest.ProcessedAtUtc,
        manifest.LineItems.Count,
        manifest.TotalGrossWeightKg,
        manifest.HazardousCount,
        manifest.ValidationErrors);
}
