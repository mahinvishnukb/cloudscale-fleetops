using System.Globalization;
using FleetOps.Domain.Manifests;

namespace FleetOps.Application.Manifests;

/// <summary>
/// Turns a raw cargo-manifest CSV into validated rows plus a per-row error list.
/// Bad rows never abort the batch — a single malformed weight should not strand
/// the other 4,000 containers on the ship.
/// </summary>
public static class ManifestCsvParser
{
    private static readonly string[] RequiredColumns =
    [
        "container_number", "description", "gross_weight_kg", "origin_port", "destination_port"
    ];

    public static ManifestParseResult Parse(string csv)
    {
        var records = Rfc4180Reader.ReadAll(csv);
        var errors = new List<ManifestRowError>();

        if (records.Count == 0)
        {
            errors.Add(new ManifestRowError(0, "file", "The manifest file is empty."));
            return new ManifestParseResult([], errors);
        }

        var header = records[0]
            .Select(h => h.Trim().ToLowerInvariant().Replace(' ', '_'))
            .ToArray();

        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < header.Length; i++)
        {
            index[header[i]] = i;
        }

        var missing = RequiredColumns.Where(c => !index.ContainsKey(c)).ToArray();
        if (missing.Length > 0)
        {
            errors.Add(new ManifestRowError(1, "header",
                $"Missing required column(s): {string.Join(", ", missing)}."));
            return new ManifestParseResult([], errors);
        }

        var rows = new List<ParsedCargoRow>();
        var seenContainers = new HashSet<string>(StringComparer.Ordinal);

        for (var r = 1; r < records.Count; r++)
        {
            var lineNumber = r + 1;
            var record = records[r];

            string Field(string column) =>
                index.TryGetValue(column, out var pos) && pos < record.Count ? record[pos] : string.Empty;

            var rawContainer = Field("container_number");
            if (!ContainerNumber.TryCreate(rawContainer, out var container))
            {
                errors.Add(new ManifestRowError(lineNumber, "container_number",
                    $"'{rawContainer}' is not a valid ISO 6346 container number."));
                continue;
            }

            if (!seenContainers.Add(container.Value))
            {
                errors.Add(new ManifestRowError(lineNumber, "container_number",
                    $"Container {container.Value} is duplicated within this manifest."));
                continue;
            }

            var rawWeight = Field("gross_weight_kg");
            if (!decimal.TryParse(rawWeight, NumberStyles.Number, CultureInfo.InvariantCulture, out var weight))
            {
                errors.Add(new ManifestRowError(lineNumber, "gross_weight_kg",
                    $"'{rawWeight}' is not a number."));
                continue;
            }

            if (weight <= 0)
            {
                errors.Add(new ManifestRowError(lineNumber, "gross_weight_kg",
                    "Gross weight must be greater than zero."));
                continue;
            }

            if (weight > 30_480)
            {
                errors.Add(new ManifestRowError(lineNumber, "gross_weight_kg",
                    $"{weight} kg exceeds the 30,480 kg maximum gross mass for a standard container."));
                continue;
            }

            var origin = Field("origin_port");
            var destination = Field("destination_port");

            if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
            {
                errors.Add(new ManifestRowError(lineNumber, "origin_port/destination_port",
                    "Both origin and destination ports are required."));
                continue;
            }

            var hazard = Field("hazard_class");

            rows.Add(new ParsedCargoRow(
                container.Value,
                Field("description"),
                weight,
                origin,
                destination,
                string.IsNullOrWhiteSpace(hazard) ? null : hazard));
        }

        return new ManifestParseResult(rows, errors);
    }
}
