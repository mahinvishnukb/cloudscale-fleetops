using System.Text.RegularExpressions;

namespace FleetOps.ManifestProcessor;

/// <summary>
/// Parses the agreed S3 key convention:
///   incoming/{IMO}/{VOYAGE}.csv
/// e.g. incoming/9074729/V-2026-014.csv
/// Encoding the routing in the key means the trigger needs no database lookup
/// to know which vessel a file belongs to.
/// </summary>
public static partial class ManifestObjectKey
{
    [GeneratedRegex(
        @"^incoming/(?<imo>\d{7})/(?<voyage>[A-Za-z0-9._-]{1,32})\.csv$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    public static bool TryParse(string objectKey, out string imo, out string voyageNumber)
    {
        imo = string.Empty;
        voyageNumber = string.Empty;

        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return false;
        }

        var match = KeyPattern().Match(objectKey.Trim());
        if (!match.Success)
        {
            return false;
        }

        imo = match.Groups["imo"].Value;
        voyageNumber = match.Groups["voyage"].Value.ToUpperInvariant();
        return true;
    }

    /// <summary>Where the processed copy is written, mirroring the incoming layout.</summary>
    public static string ToProcessedKey(string objectKey) =>
        "processed/" + objectKey["incoming/".Length..];

    public static string ToRejectedKey(string objectKey) =>
        "rejected/" + objectKey["incoming/".Length..];
}
