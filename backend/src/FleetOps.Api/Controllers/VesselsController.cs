using FleetOps.Api.Authorization;
using FleetOps.Application.Common;
using FleetOps.Application.Vessels;
using FleetOps.Domain.Vessels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetOps.Api.Controllers;

[ApiController]
[Route("api/vessels")]
[Authorize(Policy = FleetPolicies.ReadFleet)]
public sealed class VesselsController(IVesselService vessels) : ControllerBase
{
    /// <summary>Paged, filterable fleet list with the latest telemetry per vessel.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VesselSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VesselSummaryDto>>> SearchAsync(
        [FromQuery] string? search,
        [FromQuery] VesselStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await vessels.SearchAsync(search, status, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VesselDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VesselDto>> GetAsync(Guid id, CancellationToken ct)
        => Ok(await vessels.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = FleetPolicies.ManageFleet)]
    [ProducesResponseType(typeof(VesselDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VesselDto>> CreateAsync(
        [FromBody] CreateVesselRequest request, CancellationToken ct)
    {
        var created = await vessels.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = FleetPolicies.ManageFleet)]
    [ProducesResponseType(typeof(VesselDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VesselDto>> ChangeStatusAsync(
        Guid id, [FromBody] UpdateVesselStatusRequest request, CancellationToken ct)
        => Ok(await vessels.ChangeStatusAsync(id, request.Status, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = FleetPolicies.Administer)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DecommissionAsync(Guid id, CancellationToken ct)
    {
        await vessels.DecommissionAsync(id, ct);
        return NoContent();
    }
}
