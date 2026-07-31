using FleetOps.Application.Identity;
using FleetOps.Application.Manifests;
using FleetOps.Application.Telemetry;
using FleetOps.Application.Vessels;
using Microsoft.Extensions.DependencyInjection;

namespace FleetOps.Application;

public static class DependencyInjection
{
    /// <summary>Registers use-case services. Infrastructure supplies their dependencies.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IVesselService, VesselService>();
        services.AddScoped<ITelemetryService, TelemetryService>();
        services.AddScoped<IManifestIngestionService, ManifestIngestionService>();
        services.AddScoped<IAuthService, AuthService>();

        // Stateless rules engine; thresholds come from configuration.
        services.AddSingleton<AnomalyDetector>();

        return services;
    }
}
