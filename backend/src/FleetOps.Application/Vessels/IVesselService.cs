using FleetOps.Application.Common;
using FleetOps.Domain.Vessels;

namespace FleetOps.Application.Vessels;

public interface IVesselService
{
    Task<PagedResult<VesselSummaryDto>> SearchAsync(
        string? search, VesselStatus? status, int page, int pageSize, CancellationToken ct = default);

    Task<VesselDto> GetAsync(Guid id, CancellationToken ct = default);

    Task<VesselDto> CreateAsync(CreateVesselRequest request, CancellationToken ct = default);

    Task<VesselDto> ChangeStatusAsync(Guid id, VesselStatus status, CancellationToken ct = default);

    Task DecommissionAsync(Guid id, CancellationToken ct = default);
}
