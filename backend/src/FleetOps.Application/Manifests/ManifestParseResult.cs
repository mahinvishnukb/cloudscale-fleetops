namespace FleetOps.Application.Manifests;

public sealed record ManifestRowError(int LineNumber, string Column, string Message)
{
    public override string ToString() => $"Line {LineNumber} [{Column}]: {Message}";
}

public sealed record ParsedCargoRow(
    string ContainerNumber,
    string Description,
    decimal GrossWeightKg,
    string OriginPort,
    string DestinationPort,
    string? HazardClass);

public sealed record ManifestParseResult(
    IReadOnlyList<ParsedCargoRow> Rows,
    IReadOnlyList<ManifestRowError> Errors)
{
    public bool HasRows => Rows.Count > 0;

    public bool IsClean => Errors.Count == 0;
}
