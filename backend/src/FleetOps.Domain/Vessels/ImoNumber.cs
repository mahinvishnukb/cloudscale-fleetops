using System.Globalization;
using FleetOps.Domain.Common;

namespace FleetOps.Domain.Vessels;

/// <summary>
/// IMO ship identification number — seven digits where the seventh is a check digit.
/// The check digit is the units digit of (d1*7 + d2*6 + d3*5 + d4*4 + d5*3 + d6*2).
/// Modelled as a value object so an invalid IMO cannot exist anywhere in the domain.
/// </summary>
public readonly record struct ImoNumber
{
    private ImoNumber(string value) => Value = value;

    public string Value { get; }

    public static ImoNumber Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new DomainException("IMO number is required.");
        }

        var digits = raw.Trim().Replace("IMO", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

        if (digits.Length != 7 || !digits.All(char.IsAsciiDigit))
        {
            throw new DomainException($"IMO number must be exactly 7 digits; got '{raw}'.");
        }

        if (!HasValidCheckDigit(digits))
        {
            throw new DomainException($"IMO number '{digits}' failed its check-digit validation.");
        }

        return new ImoNumber(digits);
    }

    public static bool TryCreate(string? raw, out ImoNumber imo)
    {
        try
        {
            imo = Create(raw);
            return true;
        }
        catch (DomainException)
        {
            imo = default;
            return false;
        }
    }

    private static bool HasValidCheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < 6; i++)
        {
            var digit = digits[i] - '0';
            sum += digit * (7 - i);
        }

        var expected = sum % 10;
        var actual = digits[6] - '0';
        return expected == actual;
    }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
