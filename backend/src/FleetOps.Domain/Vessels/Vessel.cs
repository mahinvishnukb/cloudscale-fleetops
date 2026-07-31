using FleetOps.Domain.Common;
using FleetOps.Domain.Telemetry;

namespace FleetOps.Domain.Vessels;

/// <summary>Aggregate root for a single ship in the managed fleet.</summary>
public sealed class Vessel : Entity
{
    private readonly List<TelemetryReading> _telemetry = [];

    // EF Core materialisation constructor.
    private Vessel()
    {
        Name = string.Empty;
        HomePort = string.Empty;
        ImoNumber = string.Empty;
    }

    public Vessel(ImoNumber imo, string name, VesselType type, string homePort, int grossTonnage)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Vessel name is required.");
        }

        if (string.IsNullOrWhiteSpace(homePort))
        {
            throw new DomainException("Home port is required.");
        }

        if (grossTonnage is <= 0 or > 300_000)
        {
            throw new DomainException("Gross tonnage must be between 1 and 300,000.");
        }

        ImoNumber = imo.Value;
        Name = name.Trim();
        Type = type;
        HomePort = homePort.Trim();
        GrossTonnage = grossTonnage;
        Status = VesselStatus.InPort;
    }

    /// <summary>Stored as the raw 7-digit string; always constructed through <see cref="ImoNumber"/>.</summary>
    public string ImoNumber { get; private set; }

    /// <summary>
    /// The vessel's AIS radio identity, when known. Null for vessels registered by hand —
    /// only live AIS supplies it. Nullable rather than required because the IMO number is
    /// the durable identity; an MMSI changes when a ship re-flags.
    /// </summary>
    public string? MmsiNumber { get; private set; }

    public string Name { get; private set; }

    public VesselType Type { get; private set; }

    public VesselStatus Status { get; private set; }

    public string HomePort { get; private set; }

    public int GrossTonnage { get; private set; }

    public IReadOnlyCollection<TelemetryReading> Telemetry => _telemetry.AsReadOnly();

    public void ChangeStatus(VesselStatus status)
    {
        if (Status == VesselStatus.Decommissioned)
        {
            throw new DomainException($"Vessel {Name} is decommissioned and cannot change status.");
        }

        if (status == VesselStatus.Unknown)
        {
            throw new DomainException("Cannot set a vessel status to Unknown.");
        }

        Status = status;
        Touch();
    }

    /// <summary>
    /// Links this vessel to the AIS radio identity it broadcasts under. Idempotent for the
    /// same MMSI; a different one is allowed because ships genuinely re-flag, but it is
    /// worth logging when it happens.
    /// </summary>
    public void AssignMmsi(MmsiNumber mmsi)
    {
        if (Status == VesselStatus.Decommissioned)
        {
            throw new DomainException($"Vessel {Name} is decommissioned and cannot be assigned an MMSI.");
        }

        MmsiNumber = mmsi.Value;
        Touch();
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Vessel name is required.");
        }

        Name = name.Trim();
        Touch();
    }

    public void Decommission()
    {
        Status = VesselStatus.Decommissioned;
        Touch();
    }

    public TelemetryReading RecordTelemetry(
        DateTime recordedAtUtc,
        double latitude,
        double longitude,
        double speedOverGroundKn,
        int engineRpm,
        double fuelFlowLitresPerHour,
        double engineTempC)
    {
        if (Status == VesselStatus.Decommissioned)
        {
            throw new DomainException($"Vessel {Name} is decommissioned and cannot report telemetry.");
        }

        var reading = new TelemetryReading(
            Id,
            recordedAtUtc,
            latitude,
            longitude,
            speedOverGroundKn,
            engineRpm,
            fuelFlowLitresPerHour,
            engineTempC);

        _telemetry.Add(reading);
        return reading;
    }
}
