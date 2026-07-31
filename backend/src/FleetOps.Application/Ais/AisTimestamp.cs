using System.Globalization;

namespace FleetOps.Application.Ais;

public static class AisTimestamp
{
    // "2022-12-29 18:22:32.318353 +0000 UTC" — a Go time.Time rendered with its default
    // layout. The trailing " UTC" is a zone name, not an offset, and defeats DateTime.Parse.
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd HH:mm:ss.ffffff zzz",
        "yyyy-MM-dd HH:mm:ss.fff zzz",
        "yyyy-MM-dd HH:mm:ss zzz",
    ];

    /// <summary>
    /// Parses the feed's timestamp, falling back to the supplied clock when it is missing
    /// or malformed. A bad timestamp must never drop an otherwise usable position report.
    /// </summary>
    public static DateTime ParseOrDefault(string? raw, DateTime fallbackUtc)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallbackUtc;
        }

        var trimmed = raw.Trim();

        if (trimmed.EndsWith(" UTC", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4].Trim();
        }

        if (DateTime.TryParseExact(
                trimmed, Formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        if (DateTime.TryParse(
                trimmed, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal, out var loose))
        {
            return DateTime.SpecifyKind(loose, DateTimeKind.Utc);
        }

        return fallbackUtc;
    }
}
