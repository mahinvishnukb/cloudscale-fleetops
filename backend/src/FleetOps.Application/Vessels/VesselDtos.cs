using FleetOps.Domain.Vessels;

namespace FleetOps.Application.Vessels;

public sealed record VesselDto(
    Guid Id,
    string ImoNumber,
    string Name,
    VesselType Type,
    VesselStatus Status,
    string HomePort,
    int GrossTonnage,
    DateTime CreatedAtUtc);

public sealed record VesselSummaryDto(
    Guid Id,
    string ImoNumber,
    string Name,
    VesselType Type,
    VesselStatus Status,
    string HomePort,
    int GrossTonnage,
    double? LastSpeedKn,
    double? LastEngineTempC,
    DateTime? LastReportedAtUtc,
    int OpenAnomalyCount);

public sealed record CreateVesselRequest(
    string ImoNumber,
    string Name,
    VesselType Type,
    string HomePort,
    int GrossTonnage);

public sealed record UpdateVesselStatusRequest(VesselStatus Status);

public static class VesselMappings
{
    public static VesselDto ToDto(this Vessel vessel) => new(
        vessel.Id,
        vessel.ImoNumber,
        vessel.Name,
        vessel.Type,
        vessel.Status,
        vessel.HomePort,
        vessel.GrossTonnage,
        vessel.CreatedAtUtc);
}
