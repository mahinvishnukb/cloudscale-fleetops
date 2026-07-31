using System.Globalization;
using FleetOps.Api.Authorization;
using FleetOps.Application.Abstractions;
using FleetOps.Application.Manifests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetOps.Api.Controllers;

[ApiController]
[Route("api/manifests")]
[Authorize(Policy = FleetPolicies.ReadFleet)]
public sealed class ManifestsController(
    IManifestIngestionService ingestion,
    IManifestStorage storage,
    ILogger<ManifestsController> logger) : ControllerBase
{
    private const long MaxUploadBytes = 25 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CargoManifestDto>>> ListAsync(
        [FromQuery] Guid? vesselId, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await ingestion.ListAsync(vesselId, limit, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CargoManifestDetailDto>> GetAsync(Guid id, CancellationToken ct)
        => Ok(await ingestion.GetAsync(id, ct));

    /// <summary>
    /// Direct upload path: stores the file in S3 and ingests it synchronously.
    /// Large files should instead be dropped straight into the bucket, where the
    /// Lambda picks them up without occupying an API worker.
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Policy = FleetPolicies.ManageFleet)]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(CargoManifestDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CargoManifestDto>> UploadAsync(
        [FromForm] IFormFile file,
        [FromForm] string voyageNumber,
        [FromForm] Guid vesselId,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "No file supplied", Status = 400 });
        }

        if (file.Length > MaxUploadBytes)
        {
            return BadRequest(new ProblemDetails { Title = "File exceeds the 25 MB limit", Status = 400 });
        }

        // Date-partitioned key. Deliberately does NOT match the Lambda trigger's
        // `incoming/{IMO}/{VOYAGE}.csv` pattern: the bucket notification still fires, the
        // function sees a key it does not recognise and ignores it, so a direct upload is
        // never ingested twice.
        var objectKey = string.Create(CultureInfo.InvariantCulture,
            $"incoming/{DateTime.UtcNow:yyyy/MM/dd}/{voyageNumber}-{Guid.NewGuid():N}.csv");

        string csv;
        await using (var stream = file.OpenReadStream())
        {
            await storage.UploadAsync(objectKey, stream, "text/csv", ct);
        }

        await using (var reread = await storage.OpenReadAsync(objectKey, ct))
        using (var reader = new StreamReader(reread))
        {
            csv = await reader.ReadToEndAsync(ct);
        }

        logger.LogInformation("Manifest {Voyage} uploaded to {Key} ({Bytes} bytes)",
            voyageNumber, objectKey, file.Length);

        var result = await ingestion.IngestAsync(voyageNumber, vesselId, objectKey, csv, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
