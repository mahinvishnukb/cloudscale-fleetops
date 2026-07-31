using System.Text.Json;
using FleetOps.Application.Ais;
using Xunit;

namespace FleetOps.UnitTests.Ais;

/// <summary>
/// Parses the exact payloads published in the aisstream.io documentation, so a change in
/// the feed's shape is caught here rather than at 3am against a live socket.
/// </summary>
public sealed class AisEnvelopeParsingTests
{
    private static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private const string PositionReportPayload = """
    {
       "Message":{
          "PositionReport":{
             "Cog":308,
             "CommunicationState":81982,
             "Latitude":66.02695,
             "Longitude":12.253821666666665,
             "MessageID":1,
             "NavigationalStatus":15,
             "PositionAccuracy":true,
             "Raim":false,
             "RateOfTurn":4,
             "RepeatIndicator":0,
             "Sog":0,
             "Spare":0,
             "SpecialManoeuvreIndicator":0,
             "Timestamp":31,
             "TrueHeading":235,
             "UserID":259000420,
             "Valid":true
          }
       },
       "MessageType":"PositionReport",
       "MetaData":{
          "MMSI":259000420,
          "ShipName":"AUGUSTSON",
          "latitude":66.02695,
          "longitude":12.253821666666665,
          "time_utc":"2022-12-29 18:22:32.318353 +0000 UTC"
       }
    }
    """;

    private const string ShipStaticDataPayload = """
    {
      "MessageType": "ShipStaticData",
      "MetaData": { "MMSI": 257069200, "ShipName": "KV FARM" },
      "Message": {
        "ShipStaticData": {
          "AisVersion": 2,
          "CallSign": "LBHF",
          "Destination": "COASTGUARD@@@@@@@@H",
          "Dimension": { "A": 20, "B": 27, "C": 7, "D": 7 },
          "ImoNumber": 9353333,
          "MaximumStaticDraught": 4.5,
          "MessageID": 5,
          "Name": "KV FARM",
          "Type": 55,
          "UserID": 257069200,
          "Valid": true
        }
      }
    }
    """;

    [Fact]
    public void Parses_a_position_report()
    {
        var envelope = JsonSerializer.Deserialize<AisEnvelope>(PositionReportPayload, Options);

        Assert.NotNull(envelope);
        Assert.Equal("PositionReport", envelope!.MessageType);

        var report = envelope.Message?.PositionReport;
        Assert.NotNull(report);
        Assert.Equal(259000420, report!.UserId);
        Assert.Equal(66.02695, report.Latitude);
        Assert.Equal(0, report.Sog);
        Assert.Equal(15, report.NavigationalStatus);
        Assert.True(report.Valid);
    }

    [Fact]
    public void Position_report_metadata_carries_the_ship_name_and_time()
    {
        var envelope = JsonSerializer.Deserialize<AisEnvelope>(PositionReportPayload, Options);

        Assert.Equal("AUGUSTSON", envelope!.MetaData!.ShipName);
        Assert.Equal(2022, AisTimestamp.ParseOrDefault(envelope.MetaData.TimeUtc, DateTime.UtcNow).Year);
    }

    [Fact]
    public void Parses_ship_static_data()
    {
        var envelope = JsonSerializer.Deserialize<AisEnvelope>(ShipStaticDataPayload, Options);

        var stat = envelope!.Message?.ShipStaticData;
        Assert.NotNull(stat);
        Assert.Equal(9353333, stat!.ImoNumber);
        Assert.Equal("KV FARM", stat.Name);
        Assert.Equal(47, stat.Dimension!.LengthMetres);
        Assert.Equal(14, stat.Dimension.BeamMetres);
    }

    [Fact]
    public void Destination_padding_is_visible_to_the_caller()
    {
        // AIS pads fixed-width text fields with '@'. The ingestion service strips them;
        // this documents that the raw value really does contain them.
        // "@" as a string, not a char: a char first argument binds to xUnit's
        // IEnumerable<T> overload and the StringComparison argument then fails to convert.
        var envelope = JsonSerializer.Deserialize<AisEnvelope>(ShipStaticDataPayload, Options);
        Assert.Contains("@", envelope!.Message!.ShipStaticData!.Destination!, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_message_types_do_not_break_deserialisation()
    {
        const string payload = """
        { "MessageType": "AidsToNavigationReport", "MetaData": { "MMSI": 993682816 },
          "Message": { "AidsToNavigationReport": { "Name": "B", "Type": 26 } } }
        """;

        var envelope = JsonSerializer.Deserialize<AisEnvelope>(payload, Options);

        Assert.Equal("AidsToNavigationReport", envelope!.MessageType);
        Assert.Null(envelope.Message!.PositionReport);
        Assert.Null(envelope.Message.ShipStaticData);
    }

    [Fact]
    public void Subscription_serialises_to_the_shape_the_api_expects()
    {
        var subscription = new AisSubscription
        {
            ApiKey = "test-key",
            BoundingBoxes = new List<IReadOnlyList<IReadOnlyList<double>>>
            {
                new AisOptions.BoundingBox { South = 43.4, West = -64.5, North = 45.2, East = -62.5 }
                    .ToCornerPair(),
            },
            FilterMessageTypes = ["PositionReport", "ShipStaticData"],
        };

        var json = JsonSerializer.Serialize(subscription, Options);

        Assert.Contains("\"APIKey\":\"test-key\"", json, StringComparison.Ordinal);
        Assert.Contains("[[43.4,-64.5],[45.2,-62.5]]", json, StringComparison.Ordinal);
        Assert.Contains("PositionReport", json, StringComparison.Ordinal);
    }
}

public sealed class DerivedEngineMetricsTests
{
    private static readonly DateTime At = new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Is_deterministic_for_the_same_vessel_and_minute()
    {
        var a = DerivedEngineMetrics.From(14, "316001234", At);
        var b = DerivedEngineMetrics.From(14, "316001234", At);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Differs_between_vessels()
    {
        var a = DerivedEngineMetrics.From(14, "316001234", At);
        var b = DerivedEngineMetrics.From(14, "259000420", At);

        Assert.NotEqual(a.FuelFlowLitresPerHour, b.FuelFlowLitresPerHour);
    }

    [Fact]
    public void A_stopped_vessel_still_burns_hotel_load()
    {
        // The whole point of the near-stationary fuel-rule guard: a ship at rest is not
        // burning zero fuel, it is burning fuel to no distance.
        var metrics = DerivedEngineMetrics.From(0, "316001234", At);

        Assert.True(metrics.FuelFlowLitresPerHour > 0);
        Assert.InRange(metrics.EngineTempC, 25, 35);
    }

    [Fact]
    public void Fuel_rate_rises_steeply_with_speed()
    {
        // Hull resistance goes roughly with the cube of speed, so doubling speed costs
        // far more than double the fuel.
        var slow = DerivedEngineMetrics.From(8, "316001234", At);
        var fast = DerivedEngineMetrics.From(16, "316001234", At);

        Assert.True(fast.FuelFlowLitresPerHour > slow.FuelFlowLitresPerHour * 4);
    }

    [Fact]
    public void Economical_cruise_stays_below_the_fuel_warning_threshold()
    {
        // Otherwise every real vessel under way would alert continuously.
        var metrics = DerivedEngineMetrics.From(15, "316001234", At);
        var perNauticalMile = metrics.FuelFlowLitresPerHour / 15.0;

        Assert.InRange(perNauticalMile, 40, 120);
    }

    [Fact]
    public void Engine_temperature_stays_in_a_plausible_band()
    {
        foreach (var speed in new[] { 0, 5, 10, 15, 20, 25 })
        {
            var metrics = DerivedEngineMetrics.From(speed, "316001234", At);
            Assert.InRange(metrics.EngineTempC, 25, 90);
        }
    }

    [Fact]
    public void Never_produces_negative_values()
    {
        var metrics = DerivedEngineMetrics.From(-5, "316001234", At);

        Assert.True(metrics.EngineRpm >= 0);
        Assert.True(metrics.FuelFlowLitresPerHour >= 0);
    }
}
