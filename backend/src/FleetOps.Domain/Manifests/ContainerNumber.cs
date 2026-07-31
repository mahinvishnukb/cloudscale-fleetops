using FleetOps.Domain.Common;

namespace FleetOps.Domain.Manifests;

/// <summary>
/// ISO 6346 shipping-container identifier: 4 letters (owner code + category) followed
/// by a 6-digit serial and a check digit.
/// Check digit = (sum of char values * 2^position) mod 11, with a result of 10 mapped to 0.
/// </summary>
public readonly record struct ContainerNumber
{
    // ISO 6346 letter values skip every multiple of 11 (11, 22, 33).
    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private ContainerNumber(string value) => Value = value;

    public string Value { get; }

    public static ContainerNumber Create(string? raw)
    {
        if (!TryCreate(raw, out var container))
        {
            throw new DomainException($"'{raw}' is not a valid ISO 6346 container number.");
        }

        return container;
    }

    public static bool TryCreate(string? raw, out ContainerNumber container)
    {
        container = default;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

        if (value.Length != 11)
        {
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            if (!char.IsAsciiLetterUpper(value[i]))
            {
                return false;
            }
        }

        for (var i = 4; i < 11; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        if (ComputeCheckDigit(value[..10]) != value[10] - '0')
        {
            return false;
        }

        container = new ContainerNumber(value);
        return true;
    }

    /// <summary>Computes the ISO 6346 check digit for the first ten characters.</summary>
    public static int ComputeCheckDigit(string firstTen)
    {
        ArgumentNullException.ThrowIfNull(firstTen);

        if (firstTen.Length != 10)
        {
            throw new ArgumentException("Expected the first ten characters of a container number.", nameof(firstTen));
        }

        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var c = firstTen[i];
            var charValue = char.IsAsciiDigit(c) ? c - '0' : LetterValue(c);
            sum += charValue * (1 << i);
        }

        var check = sum % 11;
        return check == 10 ? 0 : check;
    }

    /// <summary>
    /// Letter values run from 10 upward, skipping every multiple of 11.
    /// Built once rather than derived arithmetically — the "skip 11" rule has no
    /// clean closed form and a wrong formula silently accepts bad containers.
    /// </summary>
    private static readonly int[] LetterValues = BuildLetterValues();

    private static int[] BuildLetterValues()
    {
        var values = new int[Letters.Length];
        var current = 10;
        for (var i = 0; i < Letters.Length; i++)
        {
            while (current % 11 == 0)
            {
                current++;
            }

            values[i] = current;
            current++;
        }

        return values;
    }

    private static int LetterValue(char c)
    {
        var index = Letters.IndexOf(c, StringComparison.Ordinal);
        if (index < 0)
        {
            throw new DomainException($"'{c}' is not a valid container-number character.");
        }

        return LetterValues[index];
    }

    public override string ToString() => Value;
}
