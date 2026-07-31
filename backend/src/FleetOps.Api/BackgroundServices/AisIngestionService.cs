using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FleetOps.Application.Abstractions;
using FleetOps.Application.Ais;
using FleetOps.Application.Telemetry;
using FleetOps.Domain.Vessels;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetOps.Api.BackgroundServices;

/// <summary>
/// Streams live AIS from aisstream.io and feeds it through the same application services
/// the simulator and the public API use, so real traffic exercises the real validation and
/// anomaly rules.
///
/// aisstream.io explicitly does not support browser connections: API keys are not meant to
/// be exposed to clients, and connections are throttled per key. Their recommended pattern
/// is to consume the socket server-side and relay to clients over a connection you control,
/// which is exactly what happens here — this service ingests, and SignalR fans out.
///
/// Only navigation data is real. Engine temperature, RPM and fuel flow are derived from
/// speed by <see cref="DerivedEngineMetrics"/>; AIS does not carry them.
/// </summary>
public sealed class AisIngestionService(
    IServiceScopeFactory scopeFactory,
    IOptions<AisOptions> options,
    IDateTimeProvider clock,
    ILogger<AisIngestionService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AisOptions _options = options.Value;

    /// <summary>MMSI to vessel id, for position reports whose ship we already know.</summary>
    private readonly Dictionary<string, Guid> _knownVessels = new(StringComparer.Ordinal);

    /// <summary>MMSI to the time of its last persisted reading, for throttling.</summary>
    private readonly Dictionary<string, DateTime> _lastStored = new(StringComparer.Ordinal);

    private int _messagesReceived;
    private int _readingsStored;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("AIS ingestion disabled (Ais:Enabled=false)");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogWarning(
                "AIS ingestion is enabled but Ais:ApiKey is empty. Get a free key at "
                + "https://aisstream.io/apikeys and set Ais__ApiKey. Falling back to no ingestion.");
            return;
        }

        // Ingestion is an enhancement, not a dependency. If the startup lookup fails —
        // most likely a pending migration — log it and stand down rather than letting the
        // host's default StopHost behaviour take the whole API with it.
        try
        {
            await LoadKnownVesselsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "AIS ingestion could not read the vessel table and is standing down. "
                + "If this mentions a missing column, run ./scripts/create-migration.sh and restart. "
                + "The rest of the API is unaffected.");
            return;
        }

        var attempt = 0;
        var silentDisconnects = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deliveredData = await RunConnectionAsync(stoppingToken);

                if (deliveredData)
                {
                    attempt = 0; // healthy connection; reset the backoff
                    silentDisconnects = 0;
                }
                else
                {
                    attempt++;
                    silentDisconnects++;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (InvalidOperationException ex)
            {
                // Configuration is wrong, not transient. Retrying cannot help.
                logger.LogError(ex, "AIS ingestion stopping: {Message}", ex.Message);
                return;
            }
            catch (Exception ex)
            {
                attempt++;
                silentDisconnects++;
                logger.LogError(ex, "AIS connection failed (attempt {Attempt})", attempt);
            }

            // Accepted, then dropped, with no data — three times running. The feed does not
            // send a readable error for a bad key, it simply hangs up, so this pattern is
            // the only signal available. Retrying forever would just hammer a free service.
            if (silentDisconnects >= 3)
            {
                logger.LogError(
                    "AIS closed {Count} connections without delivering any data. The most likely "
                    + "cause is an invalid Ais__ApiKey — the feed drops the socket rather than "
                    + "returning an error. Get a free key at https://aisstream.io/apikeys, or set "
                    + "Ais__Enabled=false to use the simulator. Standing down; the API is unaffected.",
                    silentDisconnects);
                return;
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            // Exponential backoff capped at a minute. The feed is beta with no SLA, so
            // reconnecting politely matters.
            var delay = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Math.Min(attempt, 6))));
            logger.LogInformation("Reconnecting to AIS in {Delay}s", delay.TotalSeconds);
            await Task.Delay(delay, stoppingToken);
        }
    }

    /// <summary>
    /// Returns true when the connection actually delivered data. A connection that is
    /// accepted and then dropped without a single message is what an invalid API key
    /// looks like from the client side — the server closes rather than replying with a
    /// readable {"error"} payload.
    /// </summary>
    private async Task<bool> RunConnectionAsync(CancellationToken ct)
    {
        var messagesAtStart = _messagesReceived;

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(_options.Endpoint), ct);

        logger.LogInformation("Connected to AIS feed at {Endpoint}", _options.Endpoint);

        // The server closes the connection if no subscription arrives within 3 seconds.
        await SendSubscriptionAsync(socket, ct);

        var buffer = new byte[32 * 1024];
        var accumulated = new MemoryStream();

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, ct);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                logger.LogWarning(
                    "AIS feed closed the connection: {Status} {Description}",
                    result.CloseStatus, result.CloseStatusDescription);
                return _messagesReceived > messagesAtStart;
            }

            accumulated.Write(buffer, 0, result.Count);

            // A single AIS message can span several frames; only parse once complete.
            if (!result.EndOfMessage)
            {
                continue;
            }

            var payload = Encoding.UTF8.GetString(accumulated.ToArray());
            accumulated.SetLength(0);

            await HandlePayloadAsync(payload, ct);
        }

        return _messagesReceived > messagesAtStart;
    }

    private async Task SendSubscriptionAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var subscription = new AisSubscription
        {
            ApiKey = _options.ApiKey,
            BoundingBoxes = _options.EffectiveBoundingBoxes.Select(b => b.ToCornerPair()).ToList(),
            FilterMessageTypes = ["PositionReport", "ShipStaticData"],
        };

        var json = JsonSerializer.Serialize(subscription, JsonOptions);
        await socket.SendAsync(
            Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, ct);

        logger.LogInformation(
            "Subscribed to {Boxes} AIS bounding box(es)", _options.EffectiveBoundingBoxes.Count);
    }

    private async Task HandlePayloadAsync(string payload, CancellationToken ct)
    {
        AisEnvelope? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<AisEnvelope>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            // The API is documented as unstable; a shape change must not kill the loop.
            logger.LogDebug(ex, "Unparseable AIS payload discarded");
            return;
        }

        if (envelope?.MessageType is null)
        {
            // An {"error": "..."} response, most often an invalid API key.
            if (payload.Contains("\"error\"", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError("AIS feed returned an error: {Payload}", payload);

                // Reconnecting on a bad key just hammers the service forever. Fail fast
                // and tell the operator exactly what to fix.
                if (payload.Contains("api key", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "aisstream.io rejected the API key. Set a valid Ais__ApiKey from "
                        + "https://aisstream.io/apikeys, or set Ais__Enabled=false to use the simulator.");
                }
            }

            return;
        }

        _messagesReceived++;

        switch (envelope.MessageType)
        {
            case "ShipStaticData":
                await HandleStaticDataAsync(envelope, ct);
                break;

            case "PositionReport":
                await HandlePositionReportAsync(envelope, ct);
                break;
        }

        if (_messagesReceived % 500 == 0)
        {
            logger.LogInformation(
                "AIS: {Received} messages received, {Stored} readings stored, {Tracked} vessels tracked",
                _messagesReceived, _readingsStored, _knownVessels.Count);
        }
    }

    private async Task HandleStaticDataAsync(AisEnvelope envelope, CancellationToken ct)
    {
        var stat = envelope.Message?.ShipStaticData;
        if (stat is null)
        {
            return;
        }

        var rawMmsi = (stat.UserId ?? envelope.MetaData?.Mmsi)?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!MmsiNumber.TryCreate(rawMmsi, out var mmsi) || _knownVessels.ContainsKey(mmsi.Value))
        {
            return;
        }

        if (_knownVessels.Count >= _options.MaxTrackedVessels)
        {
            return;
        }

        // Live AIS is full of transponders never configured with an IMO number: zeros,
        // truncated values and check-digit failures are routine. The domain rejects them,
        // which is the correct outcome — an unidentifiable hull is not a fleet vessel.
        var rawImo = stat.ImoNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!ImoNumber.TryCreate(rawImo, out var imo))
        {
            logger.LogDebug("Skipping MMSI {Mmsi}: IMO '{Imo}' failed validation", mmsi.Value, rawImo);
            return;
        }

        var name = (stat.Name ?? envelope.MetaData?.ShipName ?? string.Empty)
            .Replace("@", string.Empty, StringComparison.Ordinal) // AIS pads with '@'
            .Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();

        var existing = await db.Vessels.FirstOrDefaultAsync(v => v.ImoNumber == imo.Value, ct);
        if (existing is not null)
        {
            existing.AssignMmsi(mmsi);
            await db.SaveChangesAsync(ct);
            _knownVessels[mmsi.Value] = existing.Id;
            return;
        }

        var destination = (stat.Destination ?? string.Empty)
            .Replace("@", string.Empty, StringComparison.Ordinal).Trim();

        var vessel = new Vessel(
            imo,
            name,
            AisShipType.ToVesselType(stat.Type),
            string.IsNullOrWhiteSpace(destination) ? "At sea" : destination,
            AisShipType.EstimateGrossTonnage(stat.Dimension, stat.MaximumStaticDraught));

        vessel.AssignMmsi(mmsi);

        db.Vessels.Add(vessel);
        await db.SaveChangesAsync(ct);

        _knownVessels[mmsi.Value] = vessel.Id;

        logger.LogInformation(
            "Registered live vessel {Name} (IMO {Imo}, MMSI {Mmsi}) from AIS",
            vessel.Name, imo.Value, mmsi.Value);
    }

    private async Task HandlePositionReportAsync(AisEnvelope envelope, CancellationToken ct)
    {
        var report = envelope.Message?.PositionReport;
        if (report is null || report.Valid == false)
        {
            return;
        }

        var rawMmsi = (report.UserId ?? envelope.MetaData?.Mmsi)?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!MmsiNumber.TryCreate(rawMmsi, out var mmsi))
        {
            return;
        }

        // Positions for ships we have not yet seen static data for are dropped. Registering
        // from a position report alone would mean a vessel with no name and no IMO number.
        if (!_knownVessels.TryGetValue(mmsi.Value, out var vesselId))
        {
            return;
        }

        if (AisSentinels.Position(report.Latitude, report.Longitude) is not { } fix)
        {
            return;
        }

        var speed = AisSentinels.Speed(report.Sog);
        if (speed is null)
        {
            return;
        }

        var recordedAt = AisTimestamp.ParseOrDefault(envelope.MetaData?.TimeUtc, clock.UtcNow);

        // Under way, AIS broadcasts every 2–10 seconds. Storing all of it would swamp the
        // database without telling us anything more.
        if (_lastStored.TryGetValue(mmsi.Value, out var last) &&
            (recordedAt - last).TotalSeconds < _options.MinimumSecondsBetweenReadings)
        {
            return;
        }

        var engine = DerivedEngineMetrics.From(speed.Value, mmsi.Value, recordedAt);

        using var scope = scopeFactory.CreateScope();
        var telemetry = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();

        try
        {
            await telemetry.RecordAsync(
                vesselId,
                new RecordTelemetryRequest(
                    recordedAt,
                    fix.Latitude,
                    fix.Longitude,
                    Math.Round(speed.Value, 2),
                    engine.EngineRpm,
                    engine.FuelFlowLitresPerHour,
                    engine.EngineTempC),
                ct);

            _lastStored[mmsi.Value] = recordedAt;
            _readingsStored++;

            if (AisNavigationalStatusMap.ToVesselStatus(report.NavigationalStatus ?? 15) is { } status)
            {
                var vessel = await db.Vessels.FirstOrDefaultAsync(v => v.Id == vesselId, ct);
                if (vessel is not null && vessel.Status != status)
                {
                    vessel.ChangeStatus(status);
                    await db.SaveChangesAsync(ct);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to record AIS reading for MMSI {Mmsi}", mmsi.Value);
        }
    }

    /// <summary>Rebuilds the MMSI lookup on startup so a restart does not re-register everything.</summary>
    private async Task LoadKnownVesselsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();

        var existing = await db.Vessels
            .AsNoTracking()
            .Where(v => v.MmsiNumber != null)
            .Select(v => new { v.Id, v.MmsiNumber })
            .ToListAsync(ct);

        foreach (var vessel in existing)
        {
            _knownVessels[vessel.MmsiNumber!] = vessel.Id;
        }

        logger.LogInformation("Loaded {Count} vessel(s) with a known MMSI", _knownVessels.Count);
    }
}
