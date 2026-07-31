using FleetOps.Domain.Common;

namespace FleetOps.Domain.Manifests;

public sealed class CargoLineItem : Entity
{
    private CargoLineItem()
    {
        ContainerNumber = string.Empty;
        Description = string.Empty;
        OriginPort = string.Empty;
        DestinationPort = string.Empty;
    }

    public CargoLineItem(
        ContainerNumber containerNumber,
        string description,
        decimal grossWeightKg,
        string originPort,
        string destinationPort,
        string? hazardClass)
    {
        if (grossWeightKg <= 0)
        {
            throw new DomainException("Gross weight must be greater than zero.");
        }

        // A 40ft container's maximum gross mass is ~30,480 kg.
        if (grossWeightKg > 30_480)
        {
            throw new DomainException(
                $"Gross weight {grossWeightKg} kg exceeds the 30,480 kg maximum for a standard container.");
        }

        ContainerNumber = containerNumber.Value;
        Description = string.IsNullOrWhiteSpace(description) ? "Unspecified" : description.Trim();
        GrossWeightKg = grossWeightKg;
        OriginPort = originPort.Trim();
        DestinationPort = destinationPort.Trim();
        HazardClass = string.IsNullOrWhiteSpace(hazardClass) ? null : hazardClass.Trim();
    }

    public Guid CargoManifestId { get; private set; }

    public string ContainerNumber { get; private set; }

    public string Description { get; private set; }

    public decimal GrossWeightKg { get; private set; }

    public string OriginPort { get; private set; }

    public string DestinationPort { get; private set; }

    public string? HazardClass { get; private set; }

    public bool IsHazardous => HazardClass is not null;
}
