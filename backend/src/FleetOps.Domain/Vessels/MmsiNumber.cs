using FleetOps.Domain.Common;

namespace FleetOps.Domain.Vessels;

/// <summary>
/// Maritime Mobile Service Identity — the nine-digit identifier a vessel actually
/// broadcasts over AIS. Distinct from the IMO number: an MMSI is assigned to the radio
/// installation and changes when a ship changes flag, whereas the IMO number is assigned
/// to the hull for life. AIS position reports carry only the MMSI, so it is the join key
/// for live data even though the IMO number remains the durable identity.
///
/// The first three digits are the MID (Maritime Identification Digits), which encode the
/// flag state. The leading digit classifies the station type, and only 2–7 are ships.
/// </summary>
public readonly record struct MmsiNumber
{
    private MmsiNumber(string value) => Value = value;

    public string Value { get; }

    /// <summary>Maritime Identification Digits — the flag-state prefix.</summary>
    public int Mid => int.Parse(Value[..3], System.Globalization.CultureInfo.InvariantCulture);

    public static MmsiNumber Create(string? raw)
    {
        if (!TryCreate(raw, out var mmsi))
        {
            throw new DomainException($"'{raw}' is not a valid ship MMSI.");
        }

        return mmsi;
    }

    public static bool TryCreate(string? raw, out MmsiNumber mmsi)
    {
        mmsi = default;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();

        if (value.Length != 9 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        // Leading digit: 0 = group/coast station, 8 = handheld, 9 = aid to navigation,
        // SAR aircraft or man-overboard beacon. None of those are ships, and live AIS
        // carries plenty of them.
        var stationType = value[0] - '0';
        if (stationType is < 2 or > 7)
        {
            return false;
        }

        mmsi = new MmsiNumber(value);
        return true;
    }

    public override string ToString() => Value;
}
