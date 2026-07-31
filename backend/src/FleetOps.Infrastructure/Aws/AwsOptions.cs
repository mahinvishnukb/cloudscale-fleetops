namespace FleetOps.Infrastructure.Aws;

public sealed class AwsOptions
{
    public const string SectionName = "AWS";

    /// <summary>Override endpoint. Set to http://localstack:4566 locally; leave empty for real AWS.</summary>
    public string? ServiceUrl { get; set; }

    public string Region { get; set; } = "ca-central-1";

    public string ManifestBucket { get; set; } = "fleetops-manifests-upload-dev";

    public bool ForcePathStyle { get; set; } = true;
}
