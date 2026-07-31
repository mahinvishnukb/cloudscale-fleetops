using FleetOps.Domain.Identity;
using FleetOps.Domain.Telemetry;

namespace FleetOps.Application.Abstractions;

/// <summary>Wall-clock seam so time-dependent rules stay deterministic under test.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

/// <summary>Details of the caller on the current request.</summary>
public interface ICurrentUser
{
    string? Username { get; }

    string? Role { get; }

    bool IsInRole(string role);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(AppUser user);
}

/// <summary>Pushes live telemetry and anomalies to connected dashboards (SignalR in production).</summary>
public interface ITelemetryBroadcaster
{
    Task BroadcastReadingAsync(TelemetryReading reading, CancellationToken cancellationToken = default);

    Task BroadcastAnomalyAsync(Anomaly anomaly, CancellationToken cancellationToken = default);
}

/// <summary>Object storage seam — S3 in AWS, LocalStack locally, a fake in tests.</summary>
public interface IManifestStorage
{
    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default);

    Task<string> UploadAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken = default);
}
