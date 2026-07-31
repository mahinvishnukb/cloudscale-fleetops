using FleetOps.Domain.Common;

namespace FleetOps.Domain.Manifests;

/// <summary>
/// Aggregate root representing one uploaded cargo manifest file and everything
/// the ingestion pipeline learned about it.
/// </summary>
public sealed class CargoManifest : Entity
{
    private readonly List<CargoLineItem> _lineItems = [];
    private readonly List<string> _validationErrors = [];

    private CargoManifest()
    {
        VoyageNumber = string.Empty;
        SourceObjectKey = string.Empty;
    }

    public CargoManifest(string voyageNumber, Guid vesselId, string sourceObjectKey)
    {
        if (string.IsNullOrWhiteSpace(voyageNumber))
        {
            throw new DomainException("Voyage number is required.");
        }

        VoyageNumber = voyageNumber.Trim().ToUpperInvariant();
        VesselId = vesselId;
        SourceObjectKey = sourceObjectKey;
        Status = ManifestStatus.Pending;
        ReceivedAtUtc = DateTime.UtcNow;
    }

    public string VoyageNumber { get; private set; }

    public Guid VesselId { get; private set; }

    /// <summary>S3 object key the manifest was uploaded under.</summary>
    public string SourceObjectKey { get; private set; }

    public ManifestStatus Status { get; private set; }

    public DateTime ReceivedAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public IReadOnlyList<CargoLineItem> LineItems => _lineItems.AsReadOnly();

    public IReadOnlyList<string> ValidationErrors => _validationErrors.AsReadOnly();

    public decimal TotalGrossWeightKg => _lineItems.Sum(i => i.GrossWeightKg);

    public int HazardousCount => _lineItems.Count(i => i.IsHazardous);

    public void BeginProcessing()
    {
        if (Status is not ManifestStatus.Pending)
        {
            throw new DomainException($"Manifest {VoyageNumber} is already {Status}.");
        }

        Status = ManifestStatus.Processing;
        Touch();
    }

    public void AddLineItem(CargoLineItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_lineItems.Any(i => i.ContainerNumber == item.ContainerNumber))
        {
            throw new DomainException($"Container {item.ContainerNumber} appears more than once in this manifest.");
        }

        _lineItems.Add(item);
    }

    public void AddValidationError(string error) => _validationErrors.Add(error);

    /// <summary>
    /// Closes out processing. A manifest with no readable rows is rejected; one with
    /// rows plus row-level errors is accepted with warnings so the good cargo still moves.
    /// </summary>
    public void CompleteProcessing(DateTime processedAtUtc)
    {
        Status = (_lineItems.Count, _validationErrors.Count) switch
        {
            (0, _) => ManifestStatus.Rejected,
            (_, 0) => ManifestStatus.Accepted,
            _ => ManifestStatus.AcceptedWithWarnings,
        };

        ProcessedAtUtc = processedAtUtc;
        Touch();
    }

    public void Reject(string reason)
    {
        _validationErrors.Add(reason);
        Status = ManifestStatus.Rejected;
        ProcessedAtUtc = DateTime.UtcNow;
        Touch();
    }
}
