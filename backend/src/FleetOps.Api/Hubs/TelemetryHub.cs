using FleetOps.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetOps.Api.Hubs;

/// <summary>
/// Live telemetry feed. Clients join a per-vessel group so a dashboard watching one ship
/// is not woken by traffic from the other 500.
/// </summary>
[Authorize(Policy = FleetPolicies.ReadFleet)]
public sealed class TelemetryHub : Hub
{
    public const string Route = "/hubs/telemetry";

    public static string GroupFor(Guid vesselId) => $"vessel:{vesselId}";

    public Task SubscribeToVessel(Guid vesselId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(vesselId));

    public Task UnsubscribeFromVessel(Guid vesselId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(vesselId));
}
