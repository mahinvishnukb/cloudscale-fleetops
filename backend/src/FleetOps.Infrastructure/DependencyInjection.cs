using Amazon.Runtime;
using Amazon.S3;
using FleetOps.Application.Abstractions;
using FleetOps.Application.Telemetry;
using FleetOps.Infrastructure.Aws;
using FleetOps.Infrastructure.Identity;
using FleetOps.Infrastructure.Persistence;
using FleetOps.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FleetOps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Key),
                "Jwt:Key is required. Generate one with: openssl rand -base64 48")
            .ValidateOnStart();

        services.Configure<AwsOptions>(configuration.GetSection(AwsOptions.SectionName));
        services.Configure<AnomalyThresholds>(configuration.GetSection("Telemetry:Thresholds"));

        // AnomalyDetector is a singleton, so hand it the resolved options value directly.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AnomalyThresholds>>().Value);

        var connectionString = configuration.GetConnectionString("FleetOpsDb");

        services.AddDbContext<FleetOpsDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:FleetOpsDb is not configured. See .env.example.");
            }

            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), null);
                npgsql.MigrationsAssembly(typeof(FleetOpsDbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IFleetOpsDbContext>(sp => sp.GetRequiredService<FleetOpsDbContext>());
        services.AddScoped<DatabaseSeeder>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var aws = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
            var config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(aws.Region),
                ForcePathStyle = aws.ForcePathStyle,
            };

            if (!string.IsNullOrWhiteSpace(aws.ServiceUrl))
            {
                // LocalStack: explicit endpoint plus dummy credentials.
                config.ServiceURL = aws.ServiceUrl;
                config.AuthenticationRegion = aws.Region;
                return new AmazonS3Client(new BasicAWSCredentials("test", "test"), config);
            }

            // Real AWS: credentials come from the default chain (IRSA, env, profile).
            return new AmazonS3Client(config);
        });

        services.AddScoped<IManifestStorage, S3ManifestStorage>();

        return services;
    }
}
