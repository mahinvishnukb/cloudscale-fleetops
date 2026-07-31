using FleetOps.Api.Hubs;
using FleetOps.Application.Abstractions;
using FleetOps.Application.Telemetry;
using FleetOps.Domain.Telemetry;
using Microsoft.AspNetCore.SignalR;

namespace FleetOps.Api.Services;

public sealed class SignalRTelemetryBroadcaster(IHubContext<TelemetryHub> hub) : ITelemetryBroadcaster
{
    public async Task BroadcastReadingAsync(TelemetryReading reading, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var payload = reading.ToDto();

        // Per-vessel subscribers get the full reading; the fleet overview gets it too,
        // because the summary tiles show live speed and temperature.
        await hub.Clients.Group(TelemetryHub.GroupFor(reading.VesselId))
            .SendAsync("TelemetryReceived", payload, cancellationToken);

        await hub.Clients.All.SendAsync("FleetTelemetryReceived", payload, cancellationToken);
    }

    public async Task BroadcastAnomalyAsync(Anomaly anomaly, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anomaly);

        await hub.Clients.All.SendAsync(
            "AnomalyRaised",
            new
            {
                anomaly.Id,
                anomaly.VesselId,
                Kind = anomaly.Kind.ToString(),
                Severity = anomaly.Severity.ToString(),
                anomaly.Detail,
                anomaly.DetectedAtUtc,
            },
            cancellationToken);
    }
}
