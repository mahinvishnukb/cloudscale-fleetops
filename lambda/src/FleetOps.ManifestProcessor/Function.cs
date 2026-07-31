using System.Text;
using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.S3Events;
using Amazon.Lambda.Serialization.SystemTextJson;
using FleetOps.Application;
using FleetOps.Application.Abstractions;
using FleetOps.Application.Manifests;
using FleetOps.Domain.Manifests;
using FleetOps.Infrastructure;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Amazon.Lambda.Core and Microsoft.Extensions.Logging both define a LogLevel enum.
// This file logs through ILogger, so bind the name to that one.
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace FleetOps.ManifestProcessor;

/// <summary>
/// S3 ObjectCreated handler for cargo manifests.
///
/// Why this is a Lambda and not an API endpoint: a 40 MB manifest with 8,000 container
/// rows would occupy an API worker for the whole parse. Dropping the file straight into
/// the bucket lets the API stay responsive and lets ingestion scale independently.
///
/// It reuses the same ManifestIngestionService the API uses, so the validation rules
/// cannot drift between the two entry points.
/// </summary>
public sealed class Function
{
    private readonly ServiceProvider _services;
    private readonly ILogger<Function> _logger;

    public Function()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();

        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Information));

        services.AddApplication();
        services.AddInfrastructure(configuration);

        // The API owns identity; the Lambda never issues tokens, so a null-object
        // current-user keeps the shared services satisfied.
        services.AddSingleton<ICurrentUser, LambdaCurrentUser>();

        // Note: AddApplication() also registers ITelemetryService, which depends on
        // ITelemetryBroadcaster (a SignalR type this process has no use for). Nothing
        // here resolves it, and DI is lazy, so it is never constructed. If a future
        // change does need it, register a no-op broadcaster rather than referencing
        // ASP.NET Core from a Lambda.

        _services = services.BuildServiceProvider();
        _logger = _services.GetRequiredService<ILogger<Function>>();
    }

    /// <summary>Custom-runtime entry point. Also runnable locally: `dotnet run -- event.json`.</summary>
    public static async Task Main(string[] args)
    {
        var function = new Function();

        if (args.Length > 0 && File.Exists(args[0]))
        {
            // Local smoke test against LocalStack without deploying anything.
            var json = await File.ReadAllTextAsync(args[0]);
            var s3Event = JsonSerializer.Deserialize<S3Event>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            await function.HandleAsync(s3Event!, new LocalLambdaContext());
            return;
        }

        using var bootstrap = LambdaBootstrapBuilder
            .Create<S3Event>(function.HandleAsync, new DefaultLambdaJsonSerializer())
            .Build();

        await bootstrap.RunAsync();
    }

    public async Task HandleAsync(S3Event s3Event, ILambdaContext context)
    {
        ArgumentNullException.ThrowIfNull(s3Event);

        if (s3Event.Records is null || s3Event.Records.Count == 0)
        {
            _logger.LogWarning("Received an S3 event with no records; nothing to do");
            return;
        }

        foreach (var record in s3Event.Records)
        {
            var bucket = record.S3.Bucket.Name;
            // S3 URL-encodes keys in event notifications.
            var key = Uri.UnescapeDataString(record.S3.Object.Key.Replace('+', ' '));

            using var scope = _services.CreateScope();

            try
            {
                await ProcessOneAsync(scope.ServiceProvider, bucket, key, context);
            }
            catch (Exception ex)
            {
                // Log and rethrow: the event goes to the configured DLQ rather than
                // being silently dropped.
                _logger.LogError(ex, "Failed to process s3://{Bucket}/{Key}", bucket, key);
                throw;
            }
        }
    }

    private async Task ProcessOneAsync(
        IServiceProvider scoped, string bucket, string key, ILambdaContext context)
    {
        _logger.LogInformation(
            "Processing s3://{Bucket}/{Key} (request {RequestId})", bucket, key, context.AwsRequestId);

        if (!ManifestObjectKey.TryParse(key, out var imo, out var voyageNumber))
        {
            _logger.LogWarning(
                "Key {Key} does not match incoming/{{IMO}}/{{VOYAGE}}.csv; ignoring", key);
            return;
        }

        var storage = scoped.GetRequiredService<IManifestStorage>();
        var db = scoped.GetRequiredService<FleetOpsDbContext>();
        var ingestion = scoped.GetRequiredService<IManifestIngestionService>();

        var vesselId = await db.Vessels
            .Where(v => v.ImoNumber == imo)
            .Select(v => v.Id)
            .FirstOrDefaultAsync();

        if (vesselId == Guid.Empty)
        {
            _logger.LogWarning("No vessel registered with IMO {Imo}; rejecting {Key}", imo, key);
            await ArchiveAsync(storage, key, ManifestObjectKey.ToRejectedKey(key));
            return;
        }

        string csv;
        await using (var stream = await storage.OpenReadAsync(key))
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            csv = await reader.ReadToEndAsync();
        }

        var result = await ingestion.IngestAsync(voyageNumber, vesselId, key, csv);

        _logger.LogInformation(
            "Manifest {Voyage} for IMO {Imo}: {Status}, {Rows} row(s), {Errors} error(s), {Weight} kg total",
            result.VoyageNumber, imo, result.Status, result.LineItemCount,
            result.ValidationErrors.Count, result.TotalGrossWeightKg);

        var destination = result.Status == ManifestStatus.Rejected
            ? ManifestObjectKey.ToRejectedKey(key)
            : ManifestObjectKey.ToProcessedKey(key);

        await ArchiveAsync(storage, key, destination);
    }

    /// <summary>
    /// Copies the source object to its outcome prefix. The original is left in place so
    /// the pipeline is replayable — re-uploading is never required to reprocess.
    /// </summary>
    private async Task ArchiveAsync(IManifestStorage storage, string sourceKey, string destinationKey)
    {
        await using var source = await storage.OpenReadAsync(sourceKey);
        await storage.UploadAsync(destinationKey, source, "text/csv");

        _logger.LogInformation("Archived {Source} to {Destination}", sourceKey, destinationKey);
    }
}

internal sealed class LambdaCurrentUser : ICurrentUser
{
    public string? Username => "manifest-processor";

    public string? Role => "System";

    public bool IsInRole(string role) => false;
}

/// <summary>Stand-in context for local runs, so Main can exercise the real handler.</summary>
internal sealed class LocalLambdaContext : ILambdaContext
{
    public string AwsRequestId { get; } = Guid.NewGuid().ToString();

    public IClientContext ClientContext => null!;

    public string FunctionName => "fleetops-manifest-processor-local";

    public string FunctionVersion => "$LATEST";

    public ICognitoIdentity Identity => null!;

    public string InvokedFunctionArn => "arn:aws:lambda:ca-central-1:000000000000:function:local";

    public ILambdaLogger Logger { get; } = new ConsoleLambdaLogger();

    public string LogGroupName => "/aws/lambda/local";

    public string LogStreamName => "local";

    public int MemoryLimitInMB => 512;

    public TimeSpan RemainingTime => TimeSpan.FromMinutes(5);
}

internal sealed class ConsoleLambdaLogger : ILambdaLogger
{
    public void Log(string message) => Console.Write(message);

    public void LogLine(string message) => Console.WriteLine(message);
}
