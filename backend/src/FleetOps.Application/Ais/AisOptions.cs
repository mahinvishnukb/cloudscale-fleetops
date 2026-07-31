namespace FleetOps.Application.Ais;

public sealed class AisOptions
{
    public const string SectionName = "Ais";

    /// <summary>
    /// Off by default. With no API key the telemetry simulator runs instead, so a fresh
    /// clone works with no credentials and the demo never depends on a beta third party.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// aisstream.io API key. Supplied through configuration only — never committed.
    /// Set Ais__ApiKey in .env or your host's secret store.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Endpoint { get; set; } = "wss://stream.aisstream.io/v0/stream";

    /// <summary>
    /// Areas to subscribe to, as [south, west, north, east] in degrees. Defaults to the
    /// approaches to Halifax, Nova Scotia — a busy commercial port, and small enough to
    /// keep the message rate manageable. Subscribing to the whole world averages roughly
    /// 300 messages a second, which the service will disconnect you for not keeping up with.
    /// </summary>
    /// <summary>
    /// Deliberately empty by default. The configuration binder APPENDS to a collection
    /// that already holds items rather than replacing it, so a property initialised with
    /// a default box plus one box in appsettings.json yields two identical subscriptions.
    /// <see cref="EffectiveBoundingBoxes"/> supplies the fallback instead.
    /// </summary>
    public IReadOnlyList<BoundingBox> BoundingBoxes { get; set; } = [];

    /// <summary>Approaches to Halifax, Nova Scotia: busy, and small enough to keep up with.</summary>
    public static readonly BoundingBox DefaultBoundingBox =
        new() { South = 43.4, West = -64.5, North = 45.2, East = -62.5 };

    public IReadOnlyList<BoundingBox> EffectiveBoundingBoxes =>
        BoundingBoxes.Count > 0 ? BoundingBoxes : [DefaultBoundingBox];

    /// <summary>Cap on vessels auto-registered from the feed, so a wide box cannot flood the database.</summary>
    public int MaxTrackedVessels { get; set; } = 40;

    /// <summary>
    /// Minimum gap between stored readings per vessel. AIS broadcasts every 2–10 seconds
    /// while under way; persisting all of it would bloat the database to no benefit.
    /// </summary>
    public int MinimumSecondsBetweenReadings { get; set; } = 30;

    public sealed class BoundingBox
    {
        public double South { get; set; }

        public double West { get; set; }

        public double North { get; set; }

        public double East { get; set; }

        /// <summary>Renders as the [[lat, lon], [lat, lon]] corner pair the API expects.</summary>
        public IReadOnlyList<IReadOnlyList<double>> ToCornerPair() =>
        [
            [South, West],
            [North, East],
        ];
    }
}
