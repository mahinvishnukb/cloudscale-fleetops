namespace FleetOps.Application.Manifests;

public interface IManifestIngestionService
{
    /// <summary>
    /// Ingests one manifest. Called both by the API (direct upload) and by the
    /// S3-triggered Lambda, so the validation rules can never diverge between paths.
    /// </summary>
    Task<CargoManifestDto> IngestAsync(
        string voyageNumber, Guid vesselId, string objectKey, string csvContent, CancellationToken ct = default);

    Task<IReadOnlyList<CargoManifestDto>> ListAsync(Guid? vesselId, int limit, CancellationToken ct = default);

    Task<CargoManifestDetailDto> GetAsync(Guid manifestId, CancellationToken ct = default);
}
