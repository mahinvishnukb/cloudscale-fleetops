using Amazon.S3;
using Amazon.S3.Model;
using FleetOps.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetOps.Infrastructure.Aws;

/// <summary>
/// S3-backed manifest storage. Identical code path against real S3 and LocalStack —
/// only the endpoint and path-style flag differ, both supplied by configuration.
/// </summary>
public sealed class S3ManifestStorage(
    IAmazonS3 s3,
    IOptions<AwsOptions> options,
    ILogger<S3ManifestStorage> logger) : IManifestStorage
{
    private readonly AwsOptions _options = options.Value;

    public async Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var response = await s3.GetObjectAsync(
            new GetObjectRequest { BucketName = _options.ManifestBucket, Key = objectKey },
            cancellationToken);

        // Copy to memory so the caller is not holding an open HTTP stream.
        var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    public async Task<string> UploadAsync(
        string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.ManifestBucket,
                Key = objectKey,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            cancellationToken);

        logger.LogInformation("Uploaded manifest to s3://{Bucket}/{Key}", _options.ManifestBucket, objectKey);
        return objectKey;
    }

    public async Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var keys = new List<string>();
        string? continuationToken = null;

        do
        {
            var response = await s3.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = _options.ManifestBucket,
                    Prefix = prefix,
                    ContinuationToken = continuationToken,
                },
                cancellationToken);

            keys.AddRange(response.S3Objects.Select(o => o.Key));
            // `== true` rather than a bare bool: IsTruncated became nullable in AWSSDK v4,
            // and this form compiles against both v3 and v4.
            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return keys;
    }
}
