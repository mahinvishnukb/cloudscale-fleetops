namespace FleetOps.Domain.Manifests;

public enum ManifestStatus
{
    Pending = 0,
    Processing = 1,
    Accepted = 2,
    AcceptedWithWarnings = 3,
    Rejected = 4,
}
