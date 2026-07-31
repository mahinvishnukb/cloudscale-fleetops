using FleetOps.Application.Abstractions;
using FleetOps.Application.Telemetry;
using FleetOps.Domain.Vessels;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetOps.Api.BackgroundServices;

public sealed class SimulatorOptions
{
    public const string SectionName = "Telemetry:Simulator";

    /// <summary>Off by default; the demo environment turns it on.</summary>
    public bool Enabled { get; set; }

    public int IntervalSeconds { get; set; } = 10;

    /// <summary>Roughly one in N ticks is nudged into anomaly territory, so the alert panel is never empty.</summary>
    public int AnomalyEveryNTicks { get; set; } = 12;
}

/// <summary>
/// Stands in for the real IoT gateway: walks each vessel along a plausible track and
/// posts telemetry through the same application service the public API uses, so the
/// simulated path exercises the real validation and anomaly rules.
/// </summary>
public sealed class TelemetrySimulatorService(
    IServiceScopeFactory scopeFactory,
    IOptions<SimulatorOptions> options,
    ILogger<TelemetrySimulatorService> logger) : BackgroundService
{
    private readonly SimulatorOptions _options = options.Value;
    private readonly Random _random = new(Seed: 20260730);
    private long _tick;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Telemetry simulator is disabled (Telemetry:Simulator:Enabled=false)");
            return;
        }

        logger.LogInformation("Telemetry simulator started; interval {Interval}s", _options.IntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.IntervalSeconds)));

        while (await SafeWaitAsync(timer, stoppingToken))
        {
            try
            {
                await EmitTickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A bad tick must never take the host down.
                logger.LogError(ex, "Telemetry simulator tick failed; continuing");
            }
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task EmitTickAsync(CancellationToken ct)
    {
        _tick++;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        var telemetry = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var vessels = await db.Vessels
            .AsNoTracking()
            .Where(v => v.Status != VesselStatus.Decommissioned)
            .Select(v => new { v.Id, v.Status })
            .ToListAsync(ct);

        var forceAnomaly = _options.AnomalyEveryNTicks > 0 && _tick % _options.AnomalyEveryNTicks == 0;

        foreach (var vessel in vessels)
        {
            var last = await db.TelemetryReadings
                .AsNoTracking()
                .Where(t => t.VesselId == vessel.Id)
                .OrderByDescending(t => t.RecordedAtUtc)
                .Select(t => new { t.Latitude, t.Longitude, t.SpeedOverGroundKn })
                .FirstOrDefaultAsync(ct);

            var underWay = vessel.Status == VesselStatus.UnderWay;

            // Start somewhere in the North Atlantic if this vessel has no history yet.
            var lat = last?.Latitude ?? (44 + (_random.NextDouble() * 8));
            var lon = last?.Longitude ?? (-60 + (_random.NextDouble() * 15));

            var speed = underWay
                ? Math.Clamp((last?.SpeedOverGroundKn ?? 14) + ((_random.NextDouble() - 0.5) * 2), 6, 22)
                : Math.Round(_random.NextDouble() * 0.4, 2);

            // Advance the position by distance actually travelled this interval.
            var hours = _options.IntervalSeconds / 3600.0;
            var deltaDegrees = speed * hours / 60.0;
            lat = Math.Clamp(lat + (deltaDegrees * (_random.NextDouble() - 0.3)), -89, 89);
            lon = Math.Clamp(lon + (deltaDegrees * (_random.NextDouble() - 0.3)), -179, 179);

            var rpm = underWay ? (int)(speed * 5.5) + _random.Next(-15, 15) : _random.Next(0, 120);
            var fuelFlow = underWay ? (speed * 45) + _random.Next(-60, 60) : _random.Next(10, 40);
            var engineTemp = underWay ? 68 + (_random.NextDouble() * 12) : 30 + (_random.NextDouble() * 10);

            if (forceAnomaly && underWay)
            {
                // Deliberately overheat one vessel so the alerts panel has live content.
                engineTemp = 96 + (_random.NextDouble() * 6);
            }

            await telemetry.RecordAsync(
                vessel.Id,
                new RecordTelemetryRequest(
                    clock.UtcNow, Math.Round(lat, 5), Math.Round(lon, 5),
                    Math.Round(speed, 2), Math.Max(0, rpm), Math.Round(fuelFlow, 1), Math.Round(engineTemp, 1)),
                ct);
        }

        logger.LogDebug("Simulator tick {Tick}: emitted {Count} readings", _tick, vessels.Count);
    }
}
