using System.Text.Json.Serialization;

namespace FleetOps.Application.Ais;

/// <summary>
/// Wire types for the aisstream.io v0 websocket feed. Kept deliberately minimal: only the
/// fields this application consumes are modelled, so an upstream schema change to an
/// unrelated message type cannot break deserialisation.
///
/// The feed is documented as beta and explicitly unstable, which is another reason to
/// bind loosely and validate everything downstream.
/// </summary>
public sealed class AisEnvelope
{
    [JsonPropertyName("MessageType")]
    public string? MessageType { get; set; }

    [JsonPropertyName("MetaData")]
    public AisMetaData? MetaData { get; set; }

    [JsonPropertyName("Message")]
    public AisMessageBody? Message { get; set; }
}

public sealed class AisMetaData
{
    [JsonPropertyName("MMSI")]
    public long? Mmsi { get; set; }

    [JsonPropertyName("ShipName")]
    public string? ShipName { get; set; }

    /// <summary>
    /// Format: "2022-12-29 18:22:32.318353 +0000 UTC" — not ISO 8601, and not parseable by
    /// DateTime.Parse without help. <see cref="AisTimestamp"/> handles it.
    /// </summary>
    [JsonPropertyName("time_utc")]
    public string? TimeUtc { get; set; }
}

public sealed class AisMessageBody
{
    [JsonPropertyName("PositionReport")]
    public AisPositionReport? PositionReport { get; set; }

    [JsonPropertyName("ShipStaticData")]
    public AisShipStaticData? ShipStaticData { get; set; }
}

public sealed class AisPositionReport
{
    [JsonPropertyName("UserID")]
    public long? UserId { get; set; }

    [JsonPropertyName("Latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("Longitude")]
    public double? Longitude { get; set; }

    /// <summary>Speed over ground in knots. 102.3 means unavailable.</summary>
    [JsonPropertyName("Sog")]
    public double? Sog { get; set; }

    /// <summary>Course over ground in degrees. 360 means unavailable.</summary>
    [JsonPropertyName("Cog")]
    public double? Cog { get; set; }

    [JsonPropertyName("TrueHeading")]
    public int? TrueHeading { get; set; }

    [JsonPropertyName("NavigationalStatus")]
    public int? NavigationalStatus { get; set; }

    [JsonPropertyName("Valid")]
    public bool? Valid { get; set; }
}

public sealed class AisShipStaticData
{
    [JsonPropertyName("UserID")]
    public long? UserId { get; set; }

    /// <summary>
    /// Frequently 0 or absent on live data — plenty of transponders are never configured
    /// with the vessel's IMO number. Vessels without a valid one are not registered.
    /// </summary>
    [JsonPropertyName("ImoNumber")]
    public long? ImoNumber { get; set; }

    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("CallSign")]
    public string? CallSign { get; set; }

    /// <summary>ITU ship-type code; the tens digit gives the broad category.</summary>
    [JsonPropertyName("Type")]
    public int? Type { get; set; }

    [JsonPropertyName("Destination")]
    public string? Destination { get; set; }

    [JsonPropertyName("MaximumStaticDraught")]
    public double? MaximumStaticDraught { get; set; }

    [JsonPropertyName("Dimension")]
    public AisDimension? Dimension { get; set; }

    [JsonPropertyName("Valid")]
    public bool? Valid { get; set; }
}

/// <summary>Distances in metres from the position sensor to bow (A), stern (B), port (C), starboard (D).</summary>
public sealed class AisDimension
{
    [JsonPropertyName("A")]
    public int? A { get; set; }

    [JsonPropertyName("B")]
    public int? B { get; set; }

    [JsonPropertyName("C")]
    public int? C { get; set; }

    [JsonPropertyName("D")]
    public int? D { get; set; }

    public int LengthMetres => (A ?? 0) + (B ?? 0);

    public int BeamMetres => (C ?? 0) + (D ?? 0);
}

/// <summary>Subscription message sent within 3 seconds of opening the socket, or it is closed.</summary>
public sealed class AisSubscription
{
    [JsonPropertyName("APIKey")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>[[[lat1, lon1], [lat2, lon2]], ...] — opposite corners, order irrelevant.</summary>
    [JsonPropertyName("BoundingBoxes")]
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> BoundingBoxes { get; set; } = [];

    [JsonPropertyName("FilterMessageTypes")]
    public IReadOnlyList<string> FilterMessageTypes { get; set; } = [];
}
